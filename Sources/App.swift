import AppKit
import Carbon
import ApplicationServices
import ServiceManagement

final class ShortcutField: NSTextField {
    var record: ((NSEvent) -> Void)?
    override func mouseDown(with event: NSEvent) { window?.makeFirstResponder(self) }
    override func keyDown(with event: NSEvent) { record?(event) }
    override func performKeyEquivalent(with event: NSEvent) -> Bool {
        if window?.firstResponder === self { record?(event); return true }; return super.performKeyEquivalent(with: event)
    }
    override var acceptsFirstResponder: Bool { true }
    override func becomeFirstResponder() -> Bool { true }
}

final class HistoryPanel: NSPanel {
    var keyAction: ((UInt16) -> Bool)?
    override func sendEvent(_ event: NSEvent) {
        if event.type == .keyDown, keyAction?(event.keyCode) == true { return }
        super.sendEvent(event)
    }
}

final class ContextTable: NSTableView {
    override func menu(for event: NSEvent) -> NSMenu? {
        let index = row(at: convert(event.locationInWindow, from: nil))
        if index >= 0 { selectRowIndexes(IndexSet(integer: index), byExtendingSelection: false) }
        return super.menu(for: event)
    }
}

final class App: NSObject, NSApplicationDelegate, NSTableViewDataSource, NSTableViewDelegate, NSSearchFieldDelegate, NSWindowDelegate, NSMenuItemValidation {
    let defaults = UserDefaults.standard
    let history: History
    var statusItem: NSStatusItem!
    var panel: HistoryPanel!
    let search = NSSearchField()
    let table = ContextTable()
    let filters = NSSegmentedControl(labels: ["Todos", "Fixados", "Links", "Textos", "Imagens"], trackingMode: .selectOne, target: nil, action: nil)
    var aboutWindow: NSWindow?
    var previewWindow: NSWindow?
    let demo = CommandLine.arguments.contains("--demo") || ProcessInfo.processInfo.environment["COPILUME_DEMO"] == "1"
    let countLabel = NSTextField(labelWithString: "")
    let emptyState = NSTextField(wrappingLabelWithString: "")
    let shortcutBadge = NSTextField(labelWithString: "⌥ V")
    var copyNotice = ""
    let notice = NSTextField(wrappingLabelWithString: "")
    var visible: [Clip] = []
    let imageQueue = DispatchQueue(label: "Copilume.image-decoding", qos: .userInitiated)
    var imageProcessing = false
    var captureEpoch = 0
    let thumbnails = NSCache<NSUUID, NSImage>()
    var timer: Timer?
    var changeCount = NSPasteboard.general.changeCount
    var previousApp: NSRunningApplication?
    var hotKey: EventHotKeyRef?
    var handler: EventHandlerRef?
    var keyCode: UInt32
    var modifiers: UInt32
    var settings: NSWindow?
    var shortcutField: ShortcutField?
    var permissionLabel: NSTextField?
    var loginButton: NSButton?
    var pausedButton: NSButton!
    var fallback = false
    var persistenceWarning = false
    var pasteGeneration = 0

    override init() {
        let dir = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0].appendingPathComponent("Copilume", isDirectory: true)
        history = History(limit: UserDefaults.standard.object(forKey: "limit") as? Int ?? 200, file: dir.appendingPathComponent("history.json"))
        keyCode = UInt32(UserDefaults.standard.object(forKey: "hotKeyCode") as? Int ?? 9)
        modifiers = UInt32(UserDefaults.standard.object(forKey: "hotKeyModifiers") as? Int ?? Int(optionKey))
        super.init()
        history.persistent = defaults.bool(forKey: "persist") && !demo
    }
    func applicationDidFinishLaunching(_ notification: Notification) {
        NSApp.setActivationPolicy(.accessory)
        thumbnails.totalCostLimit = 8_000_000
        applyAppearance()
        if CommandLine.arguments.contains("--dark") { NSApp.appearance = NSAppearance(named: .darkAqua) }
        if CommandLine.arguments.contains("--light") { NSApp.appearance = NSAppearance(named: .aqua) }
        do { try history.load() } catch { alert("Não foi possível carregar o histórico salvo", "O arquivo não foi alterado. Você pode desativar o armazenamento nas preferências.") }
        buildEditMenu(); buildPanel(); buildMenu()
        var spec = EventTypeSpec(eventClass: OSType(kEventClassKeyboard), eventKind: UInt32(kEventHotKeyPressed))
        InstallEventHandler(GetApplicationEventTarget(), { _, _, context in
            guard let context else { return OSStatus(eventNotHandledErr) }
            let owner = Unmanaged<App>.fromOpaque(context).takeUnretainedValue()
            DispatchQueue.main.async { owner.togglePanel() }; return noErr
        }, 1, &spec, Unmanaged.passUnretained(self).toOpaque(), &handler)
        if !demo && !register(keyCode, modifiers) { alert("Atalho indisponível", "Não foi possível registrar \(shortcutName()). Abra Preferências para escolher outro atalho.") }
        if !demo {
            timer = Timer.scheduledTimer(withTimeInterval: 0.4, repeats: true) { [weak self] _ in self?.poll() }
            timer?.tolerance = 0.1
            RunLoop.main.add(timer!, forMode: .common)
        } else {
            _ = history.capture("Seu texto pode voltar. Sempre que precisar.", types: [])
            if let id = history.items.first?.id { history.togglePin(id) }
            _ = history.capture("https://example.com/projeto", types: [])
            _ = history.capture("Ideias para hoje\nOrganizar os atalhos\nGuardar o que importa", types: [])
            let sample = NSImage(size: NSSize(width: 640, height: 360), flipped: false) { rect in
                BrandPalette.deepBlue.setFill(); rect.fill()
                BrandPalette.cyan.setFill(); NSBezierPath(ovalIn: NSRect(x: 420, y: 150, width: 160, height: 160)).fill()
                ("Copilume" as NSString).draw(at: NSPoint(x: 40, y: 150), withAttributes: [.font: NSFont.systemFont(ofSize: 48, weight: .bold), .foregroundColor: NSColor.white])
                return true
            }
            if let data = sample.tiffRepresentation, let image = ImageClipboard.normalize(data) { history.captureImage(image, types: []) }
        }
        NSWorkspace.shared.notificationCenter.addObserver(self, selector: #selector(activatedApp(_:)), name: NSWorkspace.didActivateApplicationNotification, object: nil)
        NotificationCenter.default.addObserver(self, selector: #selector(becameActive), name: NSApplication.didBecomeActiveNotification, object: nil)
        if demo || !defaults.bool(forKey: "introduced") {
            if !demo { defaults.set(true, forKey: "introduced") }
            if !demo && Bundle.main.bundlePath.hasPrefix("/Volumes/") { about() } else { showPanel() }
        }
    }
    func applicationShouldHandleReopen(_ sender: NSApplication, hasVisibleWindows flag: Bool) -> Bool { showPanel(); return true }
    func applicationWillTerminate(_ notification: Notification) {
        timer?.invalidate(); if let hotKey { UnregisterEventHotKey(hotKey) }; if let handler { RemoveEventHandler(handler) }
    }
    func register(_ code: UInt32, _ mods: UInt32) -> Bool {
        if let hotKey { UnregisterEventHotKey(hotKey); self.hotKey = nil }
        let id = EventHotKeyID(signature: 0x43564C50, id: 1)
        return RegisterEventHotKey(code, mods, id, GetApplicationEventTarget(), UInt32(kEventHotKeyExclusive), &hotKey) == noErr
    }
    func shortcutName() -> String {
        var s = ""
        if modifiers & UInt32(controlKey) != 0 { s += "⌃" }
        if modifiers & UInt32(optionKey) != 0 { s += "⌥" }
        if modifiers & UInt32(shiftKey) != 0 { s += "⇧" }
        if modifiers & UInt32(cmdKey) != 0 { s += "⌘" }
        return s + (defaults.string(forKey: "hotKeyLabel") ?? "V")
    }
    func buildEditMenu() {
        let main = NSMenu()
        let appItem = NSMenuItem(); let appMenu = NSMenu(); appMenu.addItem(withTitle: "Sair do Copilume", action: #selector(quit), keyEquivalent: "q").target = self
        appMenu.addItem(withTitle: "Preferências…", action: #selector(openSettings), keyEquivalent: ",").target = self
        appMenu.addItem(withTitle: "Abrir histórico", action: #selector(openHistory), keyEquivalent: "").target = self
        appMenu.addItem(withTitle: "Conheça o Copilume…", action: #selector(about), keyEquivalent: "").target = self
        appItem.submenu = appMenu; main.addItem(appItem)
        let editItem = NSMenuItem(); editItem.title = "Editar"; let edit = NSMenu(title: "Editar")
        for (title, action, key) in [("Desfazer", Selector(("undo:")), "z"), ("Recortar", #selector(NSText.cut(_:)), "x"), ("Copiar", #selector(NSText.copy(_:)), "c"), ("Colar", #selector(NSText.paste(_:)), "v"), ("Selecionar Tudo", #selector(NSText.selectAll(_:)), "a")] {
            edit.addItem(withTitle: title, action: action, keyEquivalent: key)
        }
        editItem.submenu = edit; main.addItem(editItem); NSApp.mainMenu = main
    }
    @objc func activatedApp(_ note: Notification) {
        if let app = note.userInfo?[NSWorkspace.applicationUserInfoKey] as? NSRunningApplication, app.processIdentifier != ProcessInfo.processInfo.processIdentifier { previousApp = app }
    }
    func buildMenu() {
        statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.squareLength)
        statusItem.button?.image = brandMenuImage()
        let menu = NSMenu()
        for (title, action) in [("Abrir histórico (\(shortcutName()))", #selector(openHistory)), ("Pausar captura", #selector(togglePause)), ("Preferências…", #selector(openSettings)), ("Limpar histórico…", #selector(clearHistory)), ("Conheça o Copilume…", #selector(about)), ("Sair do Copilume", #selector(quit))] {
            let item = NSMenuItem(title: title, action: action, keyEquivalent: ""); item.target = self; menu.addItem(item)
        }
        statusItem.menu = menu
    }
    func buildPanel() {
        panel = HistoryPanel(contentRect: NSRect(x: 0, y: 0, width: 500, height: 640), styleMask: [.titled, .closable, .fullSizeContentView], backing: .buffered, defer: false)
        panel.title = "Copilume"; panel.titleVisibility = .hidden; panel.titlebarAppearsTransparent = true
        panel.isOpaque = false; panel.backgroundColor = .clear
        panel.isReleasedWhenClosed = false; panel.level = .floating; panel.isMovableByWindowBackground = true
        panel.collectionBehavior = [.moveToActiveSpace, .fullScreenAuxiliary]; panel.delegate = self
        let root = NSVisualEffectView(); root.material = .hudWindow; root.blendingMode = .behindWindow; root.state = .active
        panel.contentView = root
        let ambient = AmbientBackdrop(); ambient.translatesAutoresizingMaskIntoConstraints = false; root.addSubview(ambient)
        NSLayoutConstraint.activate([ambient.leadingAnchor.constraint(equalTo: root.leadingAnchor), ambient.trailingAnchor.constraint(equalTo: root.trailingAnchor), ambient.topAnchor.constraint(equalTo: root.topAnchor), ambient.bottomAnchor.constraint(equalTo: root.bottomAnchor)])
        let heading = brandLockup(iconSize: 44, titleSize: 25)
        shortcutBadge.font = .monospacedSystemFont(ofSize: 12, weight: .semibold); shortcutBadge.textColor = brandColor
        search.placeholderString = "Pesquisar no histórico"; search.delegate = self; search.controlSize = .large; search.sendsSearchStringImmediately = true
        search.setAccessibilityLabel("Pesquisar no histórico")
        table.addTableColumn(NSTableColumn(identifier: NSUserInterfaceItemIdentifier("text")))
        table.headerView = nil; table.rowHeight = 80; table.intercellSpacing = NSSize(width: 0, height: 2)
        table.backgroundColor = .clear; table.focusRingType = .none
        table.delegate = self; table.dataSource = self; table.target = self; table.action = #selector(selectItem)
        table.allowsEmptySelection = false; table.columnAutoresizingStyle = .lastColumnOnlyAutoresizingStyle
        let scroll = NSScrollView(); scroll.documentView = table; scroll.hasVerticalScroller = true; scroll.drawsBackground = false; scroll.borderType = .noBorder
        countLabel.font = .systemFont(ofSize: 11, weight: .medium); countLabel.textColor = .secondaryLabelColor
        notice.font = .systemFont(ofSize: 11); notice.textColor = .secondaryLabelColor
        emptyState.font = .systemFont(ofSize: 15, weight: .medium); emptyState.alignment = .center; emptyState.textColor = .secondaryLabelColor
        pausedButton = NSButton(title: "Pausar", target: self, action: #selector(togglePause))
        pausedButton.image = NSImage(systemSymbolName: "pause.fill", accessibilityDescription: nil); pausedButton.imagePosition = .imageLeading
        let remove = NSButton(image: NSImage(systemSymbolName: "trash", accessibilityDescription: "Excluir item")!, target: self, action: #selector(deleteItem)); remove.toolTip = "Excluir item selecionado"
        let clear = NSButton(image: NSImage(systemSymbolName: "trash.slash", accessibilityDescription: "Limpar histórico")!, target: self, action: #selector(clearHistory)); clear.toolTip = "Limpar todo o histórico"
        let prefs = NSButton(image: NSImage(systemSymbolName: "slider.horizontal.3", accessibilityDescription: "Preferências")!, target: self, action: #selector(openSettings)); prefs.toolTip = "Preferências"
        for button in [pausedButton!, remove, clear, prefs] { button.bezelStyle = .accessoryBarAction; button.controlSize = .large }
        let pin = NSButton(image: NSImage(systemSymbolName: "pin", accessibilityDescription: "Fixar ou desafixar")!, target: self, action: #selector(togglePin)); pin.toolTip = "Fixar ou desafixar item"
        let preview = NSButton(image: NSImage(systemSymbolName: "eye", accessibilityDescription: "Visualizar conteúdo")!, target: self, action: #selector(previewItem)); preview.toolTip = "Visualizar conteúdo"
        for button in [pin, preview] { button.bezelStyle = .accessoryBarAction; button.controlSize = .large }
        let context = NSMenu()
        for (title, action) in [("Fixar / desafixar", #selector(togglePin)), ("Visualizar conteúdo…", #selector(previewItem)), ("Copiar sem colar", #selector(copyOnly)), ("Copiar em uma linha", #selector(copySingleLine)), ("Excluir item", #selector(deleteItem))] { let item = NSMenuItem(title: title, action: action, keyEquivalent: ""); item.target = self; context.addItem(item) }
        table.menu = context
        let controls = NSStackView(views: [pausedButton, pin, preview, remove, clear, prefs]); controls.spacing = 8; controls.edgeInsets = NSEdgeInsets(top: 7, left: 10, bottom: 7, right: 10)
        let bar = glassSurface(controls, radius: 20)
        let author = NSButton(title: demo ? "Prévia • dados fictícios" : "Gratuito • aberto • feito para ficar no seu Mac", target: self, action: #selector(about)); author.isBordered = false; author.font = .systemFont(ofSize: 11, weight: .semibold); author.contentTintColor = .secondaryLabelColor
        filters.selectedSegment = 0; filters.target = self; filters.action = #selector(filterChanged); filters.controlSize = .large
        let headerContent = NSView()
        for view in [heading, shortcutBadge, search, filters] { view.translatesAutoresizingMaskIntoConstraints = false; headerContent.addSubview(view) }
        let header = glassSurface(headerContent, radius: 24)
        for view in [header, scroll, emptyState, countLabel, notice, bar, author] { view.translatesAutoresizingMaskIntoConstraints = false; root.addSubview(view) }
        NSLayoutConstraint.activate([
            header.topAnchor.constraint(equalTo: root.topAnchor, constant: 34), header.leadingAnchor.constraint(equalTo: root.leadingAnchor, constant: 14), header.trailingAnchor.constraint(equalTo: root.trailingAnchor, constant: -14), header.heightAnchor.constraint(equalToConstant: 176),
            heading.topAnchor.constraint(equalTo: headerContent.topAnchor, constant: 18), heading.leadingAnchor.constraint(equalTo: headerContent.leadingAnchor, constant: 18),
            shortcutBadge.trailingAnchor.constraint(equalTo: headerContent.trailingAnchor, constant: -20), shortcutBadge.centerYAnchor.constraint(equalTo: heading.centerYAnchor),
            search.topAnchor.constraint(equalTo: heading.bottomAnchor, constant: 16), search.leadingAnchor.constraint(equalTo: headerContent.leadingAnchor, constant: 16), search.trailingAnchor.constraint(equalTo: headerContent.trailingAnchor, constant: -16), search.heightAnchor.constraint(equalToConstant: 34),
            filters.topAnchor.constraint(equalTo: search.bottomAnchor, constant: 12), filters.leadingAnchor.constraint(equalTo: search.leadingAnchor), filters.trailingAnchor.constraint(equalTo: search.trailingAnchor),
            countLabel.topAnchor.constraint(equalTo: header.bottomAnchor, constant: 12), countLabel.leadingAnchor.constraint(equalTo: root.leadingAnchor, constant: 23),
            scroll.topAnchor.constraint(equalTo: countLabel.bottomAnchor, constant: 8), scroll.leadingAnchor.constraint(equalTo: root.leadingAnchor, constant: 16), scroll.trailingAnchor.constraint(equalTo: root.trailingAnchor, constant: -16),
            emptyState.centerXAnchor.constraint(equalTo: scroll.centerXAnchor), emptyState.centerYAnchor.constraint(equalTo: scroll.centerYAnchor), emptyState.widthAnchor.constraint(equalToConstant: 350),
            notice.topAnchor.constraint(equalTo: scroll.bottomAnchor, constant: 8), notice.leadingAnchor.constraint(equalTo: root.leadingAnchor, constant: 23), notice.trailingAnchor.constraint(equalTo: root.trailingAnchor, constant: -23), notice.heightAnchor.constraint(equalToConstant: 36),
            bar.topAnchor.constraint(equalTo: notice.bottomAnchor, constant: 10), bar.leadingAnchor.constraint(equalTo: root.leadingAnchor, constant: 20), bar.heightAnchor.constraint(equalToConstant: 46),
            author.topAnchor.constraint(equalTo: bar.bottomAnchor, constant: 10), author.centerXAnchor.constraint(equalTo: root.centerXAnchor), author.bottomAnchor.constraint(equalTo: root.bottomAnchor, constant: -12)
        ])
        panel.keyAction = { [weak self] key in
            guard let self else { return false }
            if let editor = self.panel.firstResponder as? NSTextView, editor.hasMarkedText() { return false }
            switch key {
            case 53: self.closePanel(); return true
            case 125: self.moveSelection(1); return true
            case 126: self.moveSelection(-1); return true
            case 36, 76: self.selectItem(); return true
            default: return false
            }
        }
    }
    func hideAuxiliaryWindows() { settings?.orderOut(nil); aboutWindow?.orderOut(nil); previewWindow?.orderOut(nil) }
    @objc func about() {
        panel.orderOut(nil); hideAuxiliaryWindows()
        if let aboutWindow { NSApp.activate(ignoringOtherApps: true); aboutWindow.makeKeyAndOrderFront(nil); return }
        let window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 520, height: 600), styleMask: [.titled, .closable], backing: .buffered, defer: false)
        window.title = "Conheça o Copilume"; window.isReleasedWhenClosed = false; aboutWindow = window
        let stack = NSStackView(); stack.orientation = .vertical; stack.alignment = .leading; stack.spacing = 14
        stack.translatesAutoresizingMaskIntoConstraints = false; window.contentView!.addSubview(stack)
        NSLayoutConstraint.activate([stack.topAnchor.constraint(equalTo: window.contentView!.topAnchor, constant: 26), stack.leadingAnchor.constraint(equalTo: window.contentView!.leadingAnchor, constant: 26), stack.trailingAnchor.constraint(equalTo: window.contentView!.trailingAnchor, constant: -26)])
        stack.addArrangedSubview(brandLockup(iconSize: 64, titleSize: 32))
        for (title, detail) in [("Copie agora. Reencontre depois.", "O Copilume devolve ao seu alcance textos, links e imagens que você já copiou. Um histórico simples, gratuito e de código aberto, inspirado na praticidade de Windows + V."), ("Um atalho para o que importa", "Option + V abre o histórico. Pesquise, navegue com as setas e escolha com Enter. Fixe textos frequentes, filtre links e veja o conteúdo completo antes de usar."), ("Seu conteúdo fica no seu Mac", "Sem conta, nuvem ou telemetria. Histórico em memória por padrão. Acessibilidade é opcional e permite colar no aplicativo anterior; sem ela, use Command + V.")] {
            let heading = NSTextField(labelWithString: title); heading.font = .systemFont(ofSize: title.hasPrefix("Copie") ? 22 : 15, weight: .semibold)
            let body = NSTextField(wrappingLabelWithString: detail); body.font = .systemFont(ofSize: 13); body.textColor = .secondaryLabelColor; body.preferredMaxLayoutWidth = 460
            stack.addArrangedSubview(heading); stack.addArrangedSubview(body)
        }
        let installed = !Bundle.main.bundlePath.hasPrefix("/Volumes/")
        let install = NSTextField(wrappingLabelWithString: installed ? "Copilume \(Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? "dev") • licença MIT • projeto de @kushhoficial" : "Você abriu pelo DMG. Arraste Copilume para Applications, ejete o disco e abra pela pasta Aplicativos para concluir a instalação.")
        install.font = .systemFont(ofSize: 11); install.preferredMaxLayoutWidth = 460; stack.addArrangedSubview(install)
        stack.addArrangedSubview(NSButton(title: "Abrir histórico", target: self, action: #selector(openHistory)))
        window.center(); NSApp.activate(ignoringOtherApps: true); window.makeKeyAndOrderFront(nil)
    }
    @objc func becameActive() { updatePermission(); if panel?.isVisible == true { refresh(keepSelection: true) } }
    @objc func openHistory() { showPanel() }
    func togglePanel() { panel.isKeyWindow && NSApp.isActive ? closePanel() : showPanel() }
    func showPanel() {
        hideAuxiliaryWindows()
        let front = NSWorkspace.shared.frontmostApplication
        if front?.processIdentifier != ProcessInfo.processInfo.processIdentifier { previousApp = front }
        fallback = false; search.stringValue = ""; filters.selectedSegment = 0; refresh()
        panel.center(); NSApp.activate(ignoringOtherApps: true); panel.makeKeyAndOrderFront(nil); panel.makeFirstResponder(search)
    }
    func closePanel() {
        panel.orderOut(nil)
        if let previousApp, !previousApp.isTerminated { previousApp.activate(options: []) }
    }
    func windowShouldClose(_ sender: NSWindow) -> Bool { if sender === panel { closePanel(); return false }; if sender === previewWindow { showPanel(); return false }; return true }
    func poll(_ pb: NSPasteboard = .general) {
        guard !imageProcessing, pb.changeCount != changeCount else { return }
        let version = pb.changeCount; changeCount = version
        guard !history.paused else { return }
        let types = Set((pb.types ?? []).map(\.rawValue))
        guard types.isDisjoint(with: History.excludedTypes) else { return }
        if let data = ImageClipboard.offeredData(on: pb) {
            guard pb.changeCount == version else { return }
            imageProcessing = true
            let epoch = captureEpoch
            imageQueue.async { [weak self] in
                let image = autoreleasepool { ImageClipboard.normalize(data) }
                DispatchQueue.main.async {
                    guard let self else { return }; self.imageProcessing = false
                    guard self.captureEpoch == epoch, !self.history.paused, pb.changeCount == version else { return }
                    if let image {
                        if self.history.captureImage(image, types: types) { self.persist(); self.refresh(keepSelection: true) }
                    } else {
                        self.fallback = true; self.copyNotice = "Imagem não guardada.\nFormato inválido ou limite de 16 MP / 8 MB excedido."; self.refresh(keepSelection: true)
                    }
                }
            }
            return
        }
        let text = pb.string(forType: .string)
        // Reject a read if the clipboard changed while its representation was being fetched.
        guard pb.changeCount == version else { return }
        if history.capture(text, types: types) { persist(); refresh(keepSelection: true) }
    }

    func persist() {
        do { try history.save(); persistenceWarning = false }
        catch { if !persistenceWarning { persistenceWarning = true; alert("Falha ao salvar histórico", "A captura continua em memória. Verifique o espaço e as permissões da pasta de dados.") } }
    }
    func refresh(keepSelection: Bool = false) {
        let selectedID = visible.indices.contains(table.selectedRow) ? visible[table.selectedRow].id : nil
        visible = history.search(search.stringValue, scope: filters.selectedSegment); table.reloadData()
        let row = keepSelection ? (visible.firstIndex { $0.id == selectedID } ?? 0) : 0
        if !visible.isEmpty { table.selectRowIndexes(IndexSet(integer: row), byExtendingSelection: false) }
        countLabel.stringValue = "\(visible.count) \(visible.count == 1 ? "item" : "itens")" + (history.paused ? "  •  captura pausada" : "  •  mais recentes primeiro")
        emptyState.isHidden = !visible.isEmpty
        emptyState.stringValue = search.stringValue.isEmpty ? "Seu próximo conteúdo começa aqui.\nCopie texto ou imagem para guardar no histórico." : "Nenhum resultado.\nExperimente outro termo."
        shortcutBadge.stringValue = shortcutName()
        notice.stringValue = fallback ? copyNotice : "↑ ↓ navegar   ↵ selecionar   esc fechar\n" + (AXIsProcessTrusted() ? "Pronto para colar no aplicativo anterior." : "Selecione para copiar. Depois, use ⌘V.")
        pausedButton.title = history.paused ? "Retomar" : "Pausar"
        statusItem?.menu?.item(at: 1)?.title = history.paused ? "Retomar captura" : "Pausar captura"
        statusItem?.button?.appearsDisabled = history.paused
    }
    func numberOfRows(in tableView: NSTableView) -> Int { visible.count }
    func tableView(_ tableView: NSTableView, rowViewForRow row: Int) -> NSTableRowView? { ClipRow() }
    func tableView(_ tableView: NSTableView, viewFor tableColumn: NSTableColumn?, row: Int) -> NSView? {
        let clip = visible[row]
        let isLink = clip.isLink
        let icon = NSImageView(image: NSImage(systemSymbolName: clip.isPinned ? "pin.fill" : (isLink ? "link" : "text.alignleft"), accessibilityDescription: nil)!)
        if let image = clip.image {
            if let cached = thumbnails.object(forKey: clip.id as NSUUID) { icon.image = cached }
            else if let thumbnail = ImageClipboard.thumbnail(image) { thumbnails.setObject(thumbnail, forKey: clip.id as NSUUID, cost: 256 * 256 * 4); icon.image = thumbnail }
            icon.imageScaling = .scaleProportionallyUpOrDown
            icon.setAccessibilityLabel(clip.displayText)
        } else { icon.contentTintColor = brandColor }
        let field = NSTextField(wrappingLabelWithString: String(clip.displayText.prefix(400)).replacingOccurrences(of: "\n", with: "  ↵  "))
        field.maximumNumberOfLines = 2; field.lineBreakMode = .byTruncatingTail; field.font = .systemFont(ofSize: 13, weight: .medium)
        let time = DateFormatter(); time.timeStyle = .short; time.dateStyle = .none
        let details = clip.image.map { "\($0.width) × \($0.height) px  ·  \(ByteCountFormatter.string(fromByteCount: Int64($0.png.count), countStyle: .file))" } ?? "\(clip.text.count) caracteres"
        let meta = NSTextField(labelWithString: "\(clip.isPinned ? "◆ " : "")\(clip.isImage ? "IMAGEM" : (isLink ? "LINK" : "TEXTO"))  ·  \(time.string(from: clip.date))  ·  \(details)")
        meta.font = .systemFont(ofSize: 10, weight: .medium); meta.textColor = .secondaryLabelColor
        let cell = NSTableCellView()
        for view in [icon, field, meta] { cell.addSubview(view); view.translatesAutoresizingMaskIntoConstraints = false }
        NSLayoutConstraint.activate([icon.leadingAnchor.constraint(equalTo: cell.leadingAnchor, constant: 16), icon.topAnchor.constraint(equalTo: cell.topAnchor, constant: 19), icon.widthAnchor.constraint(equalToConstant: clip.isImage ? 64 : 20), icon.heightAnchor.constraint(equalToConstant: clip.isImage ? 48 : 20), field.leadingAnchor.constraint(equalTo: cell.leadingAnchor, constant: clip.isImage ? 94 : 48), field.trailingAnchor.constraint(equalTo: cell.trailingAnchor, constant: -14), field.topAnchor.constraint(equalTo: cell.topAnchor, constant: 12), field.heightAnchor.constraint(lessThanOrEqualToConstant: 36), meta.leadingAnchor.constraint(equalTo: field.leadingAnchor), meta.topAnchor.constraint(equalTo: field.bottomAnchor, constant: 5), meta.trailingAnchor.constraint(lessThanOrEqualTo: cell.trailingAnchor, constant: -14)])
        cell.setAccessibilityLabel("Item do histórico"); field.setAccessibilityLabel(field.stringValue)
        return cell
    }
    func controlTextDidChange(_ obj: Notification) { if obj.object as? NSSearchField === search { refresh() } }
    func controlTextDidEndEditing(_ obj: Notification) { if let field = obj.object as? NSTextField, field.tag == 200 { changeLimit(field) } }
    func moveSelection(_ delta: Int) {
        guard !visible.isEmpty else { return }; let row = max(0, min(visible.count - 1, table.selectedRow + delta))
        table.selectRowIndexes(IndexSet(integer: row), byExtendingSelection: false); table.scrollRowToVisible(row)
    }
    @objc func selectItem() {
        guard visible.indices.contains(table.selectedRow) else { return }
        pasteGeneration += 1
        let generation = pasteGeneration
        let clip = visible[table.selectedRow]
        let pb = NSPasteboard.general
        guard ImageClipboard.write(clip, to: pb) else { alert("Não foi possível copiar", "Tente selecionar o item novamente."); return }
        changeCount = pb.changeCount
        let target = previousApp
        let trusted = AXIsProcessTrusted() && !demo
        fallback = true; copyNotice = "Copiado. Use ⌘V no destino.\nAutorize Acessibilidade nas Preferências para colar automaticamente."; refresh()
        if !trusted {
            // A small non-activating notice remains visible while the destination regains focus.
            if let target, !target.isTerminated { target.activate(options: []) }
            return
        }
        panel.orderOut(nil)
        guard let target, !target.isTerminated, target.activate(options: []) else { showCopyNotice(); return }
        waitForTarget(target, attempts: 30, generation: generation, clipboardVersion: changeCount)
    }
    func waitForTarget(_ target: NSRunningApplication, attempts: Int, generation: Int, clipboardVersion: Int) {
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.05) { [weak self] in
            guard let self else { return }
            guard generation == self.pasteGeneration, NSPasteboard.general.changeCount == clipboardVersion else { return }
            guard AXIsProcessTrusted(), !target.isTerminated else { self.showCopyNotice(); return }
            if NSWorkspace.shared.frontmostApplication?.processIdentifier == target.processIdentifier {
                // Wait for the shortcut modifiers to be released, avoiding Option+Command+V.
                if !NSEvent.modifierFlags.intersection([.command, .option, .control, .shift]).isEmpty {
                    if attempts > 0 { self.waitForTarget(target, attempts: attempts - 1, generation: generation, clipboardVersion: clipboardVersion) } else { self.showCopyNotice() }; return
                }
                let source = CGEventSource(stateID: .combinedSessionState)
                guard let down = CGEvent(keyboardEventSource: source, virtualKey: 9, keyDown: true), let up = CGEvent(keyboardEventSource: source, virtualKey: 9, keyDown: false) else { self.showCopyNotice(); return }
                down.flags = .maskCommand; up.flags = .maskCommand
                down.post(tap: .cghidEventTap); up.post(tap: .cghidEventTap)
            } else if attempts > 0 { self.waitForTarget(target, attempts: attempts - 1, generation: generation, clipboardVersion: clipboardVersion) }
            else { self.showCopyNotice() }
        }
    }
    func showCopyNotice() { fallback = true; copyNotice = "Copiado. Use ⌘V no destino.\nNão foi possível concluir a colagem automática."; refresh(); panel.orderFrontRegardless() }
    @objc func togglePause() {
        captureEpoch += 1; history.paused.toggle(); changeCount = NSPasteboard.general.changeCount; refresh()
    }
    @objc func filterChanged() { refresh() }
    var selectedClip: Clip? { visible.indices.contains(table.selectedRow) ? visible[table.selectedRow] : nil }
    @objc func togglePin() { guard let clip = selectedClip else { return }; history.togglePin(clip.id); persist(); refresh(keepSelection: true) }
    @objc func copyOnly() { copySelected(singleLine: false) }
    @objc func copySingleLine() { copySelected(singleLine: true) }
    func copySelected(singleLine: Bool) {
        guard let clip = selectedClip else { return }; pasteGeneration += 1
        let pb = NSPasteboard.general
        guard ImageClipboard.write(clip, to: pb, singleLine: singleLine) else { return }
        changeCount = pb.changeCount; fallback = true; copyNotice = singleLine ? "Copiado em uma linha.\nUse ⌘V onde quiser." : "Copiado sem colar.\nUse ⌘V onde quiser."; refresh(keepSelection: true)
    }
    func validateMenuItem(_ menuItem: NSMenuItem) -> Bool {
        if menuItem.action == #selector(copySingleLine) { return selectedClip.map { !$0.isImage } ?? false }
        return true
    }
    @objc func previewItem() {
        guard let clip = selectedClip else { return }
        panel.orderOut(nil); hideAuxiliaryWindows()
        let window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 620, height: 440), styleMask: [.titled, .closable, .resizable], backing: .buffered, defer: false)
        window.title = clip.isImage ? "Imagem • Copilume" : "Texto completo • Copilume"; window.delegate = self; window.isReleasedWhenClosed = false; window.minSize = NSSize(width: 400, height: 260)
        if let image = clip.image, let preview = ImageClipboard.thumbnail(image, maxSize: 1600) {
            let view = NSImageView(image: preview); view.imageScaling = .scaleProportionallyUpOrDown
            view.frame = window.contentView!.bounds.insetBy(dx: 16, dy: 16); view.autoresizingMask = [.width, .height]
            view.setAccessibilityLabel(clip.displayText); window.contentView!.addSubview(view)
        } else {
            let scroll = NSScrollView(frame: window.contentView!.bounds); scroll.autoresizingMask = [.width, .height]; scroll.hasVerticalScroller = true
            let text = NSTextView(frame: scroll.bounds); text.isEditable = false; text.isSelectable = true; text.isRichText = false; text.font = .systemFont(ofSize: 14); text.textContainerInset = NSSize(width: 20, height: 20)
            text.isVerticallyResizable = true; text.isHorizontallyResizable = false; text.autoresizingMask = [.width]; text.textContainer?.widthTracksTextView = true; text.string = clip.isImage ? "Não foi possível abrir esta imagem." : clip.text
            scroll.documentView = text; window.contentView!.addSubview(scroll)
        }
        previewWindow = window
        window.center(); NSApp.activate(ignoringOtherApps: true); window.makeKeyAndOrderFront(nil)
    }
    func applyAppearance() {
        switch defaults.integer(forKey: "appearance") { case 1: NSApp.appearance = NSAppearance(named: .aqua); case 2: NSApp.appearance = NSAppearance(named: .darkAqua); default: NSApp.appearance = nil }
    }
    @objc func changeAppearance(_ sender: NSPopUpButton) { if !demo { defaults.set(sender.indexOfSelectedItem, forKey: "appearance") }; switch sender.indexOfSelectedItem { case 1: NSApp.appearance = NSAppearance(named: .aqua); case 2: NSApp.appearance = NSAppearance(named: .darkAqua); default: NSApp.appearance = nil } }
    @objc func deleteItem() {
        guard visible.indices.contains(table.selectedRow) else { return }
        thumbnails.removeObject(forKey: visible[table.selectedRow].id as NSUUID); history.remove(visible[table.selectedRow].id); persist(); refresh()
    }
    @objc func clearHistory() {
        let a = NSAlert(); a.messageText = "Limpar todo o histórico?"; a.informativeText = "Limpar recentes preserva os fixados. Limpar tudo também remove os fixados. O clipboard atual não é alterado."; a.addButton(withTitle: "Cancelar"); a.addButton(withTitle: "Limpar recentes"); a.addButton(withTitle: "Limpar tudo, incluindo fixados")
        NSApp.activate(ignoringOtherApps: true)
        let answer = a.runModal(); if answer == .alertSecondButtonReturn { thumbnails.removeAllObjects(); history.clearUnpinned(); persist(); refresh() } else if answer == .alertThirdButtonReturn { thumbnails.removeAllObjects(); history.clear(); persist(); refresh() }
    }
    @objc func openSettings() {
        panel.orderOut(nil); aboutWindow?.orderOut(nil); previewWindow?.orderOut(nil)
        if let settings { updatePermission(); settings.makeKeyAndOrderFront(nil); NSApp.activate(ignoringOtherApps: true); return }
        let window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 550, height: 690), styleMask: [.titled, .closable], backing: .buffered, defer: false)
        window.title = "Preferências do Copilume"; window.isReleasedWhenClosed = false; settings = window
        let stack = NSStackView(); stack.orientation = .vertical; stack.alignment = .leading; stack.spacing = 12; stack.translatesAutoresizingMaskIntoConstraints = false
        window.contentView!.addSubview(stack)
        NSLayoutConstraint.activate([stack.leadingAnchor.constraint(equalTo: window.contentView!.leadingAnchor, constant: 22), stack.trailingAnchor.constraint(equalTo: window.contentView!.trailingAnchor, constant: -22), stack.topAnchor.constraint(equalTo: window.contentView!.topAnchor, constant: 22)])
        func label(_ text: String) -> NSTextField { let v = NSTextField(wrappingLabelWithString: text); v.font = .systemFont(ofSize: 12); v.preferredMaxLayoutWidth = 460; return v }
        stack.addArrangedSubview(brandLockup(iconSize: 44, titleSize: 26))
        stack.addArrangedSubview(label("ATALHO GLOBAL — clique no campo e pressione a combinação desejada"))
        let field = ShortcutField(); field.stringValue = shortcutName(); field.isEditable = false; field.isSelectable = false; field.isBezeled = true; field.focusRingType = .exterior
        field.widthAnchor.constraint(equalToConstant: 220).isActive = true; field.heightAnchor.constraint(equalToConstant: 28).isActive = true
        field.record = { [weak self] event in self?.changeShortcut(event) }; shortcutField = field; stack.addArrangedSubview(field)
        let limitField = NSTextField(string: String(history.limit)); limitField.widthAnchor.constraint(equalToConstant: 80).isActive = true
        limitField.delegate = self; limitField.target = self; limitField.action = #selector(changeLimit(_:)); limitField.tag = 200
        let row = NSStackView(views: [label("Máximo de itens (1–5000):"), limitField]); row.spacing = 12; stack.addArrangedSubview(row)
        let save = NSButton(checkboxWithTitle: "Salvar histórico entre reinicializações", target: self, action: #selector(togglePersistence(_:))); save.state = history.persistent ? .on : .off; save.isEnabled = !demo; stack.addArrangedSubview(save)
        stack.addArrangedSubview(label("Desativado por padrão. Quando ativado, salva textos e imagens em arquivo local sem criptografia própria, protegido pelas permissões da sua conta. Desativar apaga o arquivo salvo."))
        let login = NSButton(checkboxWithTitle: "Iniciar ao entrar no macOS", target: self, action: #selector(toggleLogin(_:))); loginButton = login; login.isEnabled = !demo; stack.addArrangedSubview(login)
        let permission = label(""); permissionLabel = permission; stack.addArrangedSubview(permission)
        let buttons = NSStackView(views: [NSButton(title: "Autorizar colagem…", target: self, action: #selector(openAccessibility)), NSButton(title: "Verificar permissão", target: self, action: #selector(checkPermission))]); buttons.spacing = 8; stack.addArrangedSubview(buttons)
        stack.addArrangedSubview(label("No macOS 27: Ajustes do Sistema → Privacidade e Segurança → Controle do Dispositivo e Acesso a Dados. Nas versões anteriores: Acessibilidade. Adicione o Copilume instalado e ative a chave. Sem essa autorização, use ⌘V manualmente."))
        stack.addArrangedSubview(label("Tudo local. Sem rede nem telemetria. Marcações conhecidas de conteúdo confidencial são ignoradas; isso não detecta toda senha. Textos: até 2 MB. Imagens: PNG/TIFF/JPEG, até 16 megapixels e 8 MB após conversão. Histórico total: 24 MB."))
        let appearance = NSPopUpButton()
        appearance.addItems(withTitles: ["Aparência do sistema", "Claro", "Escuro"]); appearance.selectItem(at: defaults.integer(forKey: "appearance")); appearance.target = self; appearance.action = #selector(changeAppearance(_:)); stack.addArrangedSubview(appearance)
        if NSWorkspace.shared.accessibilityDisplayShouldReduceTransparency { stack.addArrangedSubview(label("A transparência está reduzida nos ajustes de Acessibilidade do macOS. O Copilume respeita essa preferência.")) }
        updatePermission(); window.center(); window.makeKeyAndOrderFront(nil); NSApp.activate(ignoringOtherApps: true)
    }
    func changeShortcut(_ event: NSEvent) {
        guard !demo else { return }
        if event.keyCode == 53 { settings?.makeFirstResponder(nil); return }
        let flags = event.modifierFlags.intersection(.deviceIndependentFlagsMask)
        guard !flags.intersection([.command, .option, .control]).isEmpty else { alert("Use um modificador", "Inclua Option, Control ou Command no atalho."); return }
        var mods: UInt32 = 0
        if flags.contains(.command) { mods |= UInt32(cmdKey) }; if flags.contains(.option) { mods |= UInt32(optionKey) }; if flags.contains(.control) { mods |= UInt32(controlKey) }; if flags.contains(.shift) { mods |= UInt32(shiftKey) }
        let oldCode = keyCode, oldMods = modifiers
        guard register(UInt32(event.keyCode), mods) else {
            let restored = register(oldCode, oldMods)
            alert("Atalho indisponível", restored ? "Outro aplicativo ou o sistema pode estar usando essa combinação. O atalho anterior foi mantido." : "Não foi possível restaurar o atalho anterior. Escolha outra combinação.")
            return
        }
        keyCode = UInt32(event.keyCode); modifiers = mods
        defaults.set(Int(keyCode), forKey: "hotKeyCode"); defaults.set(Int(modifiers), forKey: "hotKeyModifiers")
        defaults.set(event.charactersIgnoringModifiers?.uppercased() ?? "Tecla \(event.keyCode)", forKey: "hotKeyLabel")
        shortcutField?.stringValue = shortcutName(); statusItem.menu?.item(at: 0)?.title = "Abrir histórico (\(shortcutName()))"
        settings?.makeFirstResponder(nil)
    }
    @objc func changeLimit(_ sender: NSTextField) {
        guard let value = Int(sender.stringValue), (1...5000).contains(value) else { sender.stringValue = String(history.limit); alert("Limite inválido", "Digite um número entre 1 e 5000."); return }
        history.limit = value; if !demo { defaults.set(value, forKey: "limit") }; persist(); refresh()
    }
    @objc func togglePersistence(_ sender: NSButton) {
        history.persistent = sender.state == .on
        do {
            if history.persistent { try history.save() } else { try history.deleteSaved() }
            defaults.set(history.persistent, forKey: "persist")
        } catch {
            history.persistent.toggle(); sender.state = history.persistent ? .on : .off
            alert("Não foi possível alterar o armazenamento", "Verifique o espaço e as permissões da pasta ~/Library/Application Support/Copilume.")
        }
    }
    @objc func toggleLogin(_ sender: NSButton) {
        do {
            if sender.state == .on { try SMAppService.mainApp.register() } else { try SMAppService.mainApp.unregister() }
            if SMAppService.mainApp.status == .requiresApproval { alert("Aprovação necessária", "Autorize Copilume em Ajustes do Sistema → Geral → Itens de Início.") }
        } catch { alert("Não foi possível alterar o início automático", "O macOS recusou o registro. Verifique Ajustes do Sistema → Geral → Itens de Início. Detalhe: \(error.localizedDescription)") }
        updatePermission()
    }
    func updatePermission() {
        permissionLabel?.stringValue = AXIsProcessTrusted() ? "✓ Acessibilidade concedida: colagem automática disponível." : "Acessibilidade não concedida: seleção copia para usar com ⌘V."
        loginButton?.state = SMAppService.mainApp.status == .enabled || SMAppService.mainApp.status == .requiresApproval ? .on : .off
    }
    @objc func checkPermission() { updatePermission() }
    @objc func openAccessibility() {
        guard !demo else { alert("Prévia visual", "Esta prévia usa dados fictícios e não envia teclas para outros aplicativos."); return }
        let options = [kAXTrustedCheckOptionPrompt.takeUnretainedValue() as String: true] as CFDictionary
        _ = AXIsProcessTrustedWithOptions(options)
        NSWorkspace.shared.open(URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility")!) }
    func alert(_ title: String, _ detail: String) { let a = NSAlert(); a.messageText = title; a.informativeText = detail; a.addButton(withTitle: "OK"); NSApp.activate(ignoringOtherApps: true); a.runModal() }
    @objc func quit() { NSApp.terminate(nil) }
}
