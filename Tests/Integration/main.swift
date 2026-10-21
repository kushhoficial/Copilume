import AppKit
import ImageIO
import UniformTypeIdentifiers
let application = NSApplication.shared
application.setActivationPolicy(.prohibited)
let owner = App()
owner.history.persistent = false
owner.table.addTableColumn(NSTableColumn(identifier: NSUserInterfaceItemIdentifier("test")))
owner.table.dataSource = owner
owner.pausedButton = NSButton()
let board = NSPasteboard.withUniqueName()
defer { board.releaseGlobally() }
var checks = 0
func check(_ condition: @autoclosure () -> Bool, _ label: String) {
    precondition(condition(), label); checks += 1; print("PASS: \(label)")
}
func finishDecode() {
    let deadline = Date().addingTimeInterval(5)
    while owner.imageProcessing && Date() < deadline { RunLoop.current.run(until: Date().addingTimeInterval(0.01)) }
    precondition(!owner.imageProcessing, "image worker timeout")
}
let bitmap = NSBitmapImageRep(bitmapDataPlanes: nil, pixelsWide: 30, pixelsHigh: 20, bitsPerSample: 8, samplesPerPixel: 4, hasAlpha: true, isPlanar: false, colorSpaceName: .deviceRGB, bytesPerRow: 0, bitsPerPixel: 0)!
memset(bitmap.bitmapData!, 127, bitmap.bytesPerRow * bitmap.pixelsHigh)
let png = bitmap.representation(using: .png, properties: [:])!
board.clearContents(); board.setData(png, forType: .png); board.setString("image fallback text", forType: .string)
owner.changeCount = board.changeCount - 1
owner.poll(board); finishDecode()
check(owner.history.items.count == 1 && owner.history.items[0].isImage, "real poll prioritizes raster over accompanying text")
check(owner.history.items[0].image?.width == 30, "background conversion reaches history")
owner.poll(board)
check(owner.history.items.count == 1, "unchanged clipboard is not recaptured")
board.clearContents(); board.setData(png, forType: .png)
owner.poll(board); finishDecode()
check(owner.history.items.count == 1, "same image from another change is deduplicated")
board.clearContents(); board.setData(png, forType: .png); board.setString("", forType: NSPasteboard.PasteboardType("org.nspasteboard.ConcealedType"))
owner.poll(board)
check(!owner.imageProcessing && owner.history.items.count == 1, "real poll rejects confidential image before decoding")
owner.history.paused = true
board.clearContents(); board.setString("paused synthetic text", forType: .string)
owner.poll(board)
check(owner.history.items.count == 1, "real poll honors pause")
owner.history.paused = false
board.clearContents(); board.setData(png, forType: .png)
owner.poll(board)
board.clearContents(); board.setString("newer synthetic item", forType: .string)
finishDecode()
check(owner.history.items.count == 1, "stale background image result discarded after clipboard changes")
owner.poll(board)
check(owner.history.items.first?.text == "newer synthetic item", "latest clipboard captured after worker completes")
board.clearContents(); board.setData(png, forType: .png)
owner.poll(board); owner.captureEpoch += 1
finishDecode()
check(!owner.history.items[0].isImage, "pause epoch invalidates pending capture")
board.clearContents(); board.setData(Data([1,2,3]), forType: .png)
owner.poll(board); finishDecode()
check(owner.fallback && owner.copyNotice.contains("não guardada"), "invalid image gives notice without storing bogus text")
print("\(checks) application integration checks passed; private clipboard only")
