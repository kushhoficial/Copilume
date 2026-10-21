import AppKit

enum BrandPalette {
    static let deepBlue = NSColor(srgbRed: 11/255, green: 42/255, blue: 68/255, alpha: 1)
    static let petroleum = NSColor(srgbRed: 14/255, green: 116/255, blue: 144/255, alpha: 1)
    static let cyan = NSColor(srgbRed: 56/255, green: 189/255, blue: 248/255, alpha: 1)
    static let ice = NSColor(srgbRed: 230/255, green: 246/255, blue: 255/255, alpha: 1)
    static let slate = NSColor(srgbRed: 148/255, green: 163/255, blue: 187/255, alpha: 1)
    static let ink = NSColor(name: NSColor.Name("CopilumeInk")) { appearance in
        appearance.bestMatch(from: [.darkAqua, .aqua]) == .darkAqua ? ice : deepBlue
    }
}
let brandColor = NSColor(name: NSColor.Name("CopilumeAccent")) { appearance in
    appearance.bestMatch(from: [.darkAqua, .aqua]) == .darkAqua ? BrandPalette.cyan : BrandPalette.petroleum
}

private enum BrandResources {
    static let icon: NSImage = {
        guard let url = Bundle.main.url(forResource: "AppIcon", withExtension: "png"), let image = NSImage(contentsOf: url) else {
            preconditionFailure("Official Copilume icon is missing from Resources")
        }
        return image
    }()
}

func brandImage(size: CGFloat) -> NSImage {
    NSImage(size: NSSize(width: size, height: size), flipped: false) { rect in
        BrandResources.icon.draw(in: rect, from: .zero, operation: .sourceOver, fraction: 1)
        return true
    }
}

func brandLockup(iconSize: CGFloat = 42, titleSize: CGFloat = 23) -> NSView {
    let icon = NSImageView(image: brandImage(size: iconSize)); icon.imageScaling = .scaleProportionallyUpOrDown
    icon.widthAnchor.constraint(equalToConstant: iconSize).isActive = true; icon.heightAnchor.constraint(equalToConstant: iconSize).isActive = true
    let title = NSTextField(labelWithString: "Copilume")
    let name = NSMutableAttributedString(string: "Copilume", attributes: [.font: NSFont.systemFont(ofSize: titleSize, weight: .bold), .foregroundColor: BrandPalette.ink, .kern: -0.5])
    name.addAttribute(.foregroundColor, value: BrandPalette.cyan, range: NSRange(location: 4, length: 4)); title.attributedStringValue = name
    title.setAccessibilityLabel("Copilume")
    let credit = NSTextField(labelWithString: "por kushhoficial")
    let signature = NSMutableAttributedString(string: "por kushhoficial", attributes: [.font: NSFont.systemFont(ofSize: 11), .foregroundColor: NSColor.secondaryLabelColor])
    signature.addAttributes([.font: NSFont.systemFont(ofSize: 11, weight: .semibold), .foregroundColor: BrandPalette.ink], range: NSRange(location: 4, length: 12))
    credit.attributedStringValue = signature
    let text = NSStackView(views: [title, credit]); text.orientation = .vertical; text.alignment = .leading; text.spacing = 1
    let lockup = NSStackView(views: [icon, text]); lockup.spacing = 10; lockup.alignment = .centerY
    lockup.setAccessibilityLabel("Copilume, por kushhoficial")
    return lockup
}

/// Optical monochrome adaptation of the supplied three-sheet / return-arrow mark.
/// Geometry is code-native so it stays sharp at menu-bar sizes and has no white background.
final class MenuBrandGlyph: NSView {
    override var isOpaque: Bool { false }
    override func draw(_ dirtyRect: NSRect) {
        NSGraphicsContext.saveGraphicsState()
        let transform = NSAffineTransform(); transform.scaleX(by: bounds.width / 24, yBy: bounds.height / 24); transform.concat()
        NSColor.black.setStroke(); NSColor.black.setFill()
        func sheet(_ rect: NSRect, angle: CGFloat) {
            NSGraphicsContext.saveGraphicsState()
            let rotate = NSAffineTransform(); rotate.translateX(by: rect.midX, yBy: rect.midY); rotate.rotate(byDegrees: angle); rotate.translateX(by: -rect.midX, yBy: -rect.midY); rotate.concat()
            let path = NSBezierPath(roundedRect: rect, xRadius: 2, yRadius: 2); path.lineWidth = 1.65; path.stroke()
            NSGraphicsContext.restoreGraphicsState()
        }
        sheet(NSRect(x: 3, y: 9, width: 12, height: 12), angle: 14)
        sheet(NSRect(x: 6, y: 7, width: 12, height: 12), angle: 8)
        sheet(NSRect(x: 9, y: 5.5, width: 12, height: 12), angle: 0)
        let curve = NSBezierPath(); curve.move(to: NSPoint(x: 3, y: 10)); curve.curve(to: NSPoint(x: 14, y: 3), controlPoint1: NSPoint(x: 0, y: 3), controlPoint2: NSPoint(x: 7, y: 2.8)); curve.lineWidth = 2.5; curve.lineCapStyle = .round; curve.stroke()
        let arrow = NSBezierPath(); arrow.move(to: NSPoint(x: 13, y: 0.2)); arrow.line(to: NSPoint(x: 18, y: 3.1)); arrow.line(to: NSPoint(x: 13, y: 6.3)); arrow.close(); arrow.fill()
        NSGraphicsContext.restoreGraphicsState()
    }
}

func brandMenuImage() -> NSImage {
    guard let url = Bundle.main.url(forResource: "MenuIconTemplate", withExtension: "pdf"), let image = NSImage(contentsOf: url) else {
        preconditionFailure("Official Copilume menu icon is missing from Resources")
    }
    image.size = NSSize(width: 18, height: 18); image.isTemplate = true; image.accessibilityDescription = "Copilume — histórico da área de transferência"
    return image
}

func glassSurface(_ content: NSView, radius: CGFloat = 20) -> NSView {
    if #available(macOS 26.0, *) {
        let glass = NSGlassEffectView(); glass.style = .regular; glass.cornerRadius = radius; glass.tintColor = brandColor.withAlphaComponent(0.10)
        glass.contentView = content
        if #available(macOS 27.0, *) { glass.effectIsInteractive = true }
        return glass
    }
    let effect = NSVisualEffectView(); effect.material = .popover; effect.state = .active
    effect.wantsLayer = true; effect.layer?.cornerRadius = radius
    effect.addSubview(content); content.translatesAutoresizingMaskIntoConstraints = false
    NSLayoutConstraint.activate([content.leadingAnchor.constraint(equalTo: effect.leadingAnchor), content.trailingAnchor.constraint(equalTo: effect.trailingAnchor), content.topAnchor.constraint(equalTo: effect.topAnchor), content.bottomAnchor.constraint(equalTo: effect.bottomAnchor)])
    return effect
}

final class ClipRow: NSTableRowView {
    override func drawBackground(in dirtyRect: NSRect) {
        NSColor.controlBackgroundColor.withAlphaComponent(0.72).setFill()
        NSBezierPath(roundedRect: bounds.insetBy(dx: 2, dy: 3), xRadius: 13, yRadius: 13).fill()
    }
    override func drawSelection(in dirtyRect: NSRect) {
        brandColor.withAlphaComponent(0.16).setFill()
        let path = NSBezierPath(roundedRect: bounds.insetBy(dx: 2, dy: 3), xRadius: 13, yRadius: 13)
        path.fill(); brandColor.withAlphaComponent(0.55).setStroke(); path.lineWidth = 1; path.stroke()
    }
    override var interiorBackgroundStyle: NSView.BackgroundStyle { .normal }
}

/// A restrained colored backdrop gives the native glass real content to refract.
/// Text cards remain on semantic system colors for readability in both appearances.
final class AmbientBackdrop: NSView {
    override func draw(_ dirtyRect: NSRect) {
        guard !NSWorkspace.shared.accessibilityDisplayShouldReduceTransparency else { return }
        let dark = effectiveAppearance.bestMatch(from: [.darkAqua, .aqua]) == .darkAqua
        let first = NSPoint(x: bounds.width * 0.08, y: bounds.height * 0.92)
        NSGradient(starting: brandColor.withAlphaComponent(dark ? 0.26 : 0.19), ending: brandColor.withAlphaComponent(0))?.draw(fromCenter: first, radius: 0, toCenter: first, radius: bounds.height * 0.85, options: [])
        let second = NSPoint(x: bounds.width * 0.95, y: bounds.height * 0.18)
        let blue = BrandPalette.cyan
        NSGradient(starting: blue.withAlphaComponent(dark ? 0.15 : 0.08), ending: blue.withAlphaComponent(0))?.draw(fromCenter: second, radius: 0, toCenter: second, radius: bounds.width * 0.95, options: [])
    }
    override func viewDidChangeEffectiveAppearance() { super.viewDidChangeEffectiveAppearance(); needsDisplay = true }
}
