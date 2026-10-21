import Foundation

var checks = 0
func check(_ condition: @autoclosure () -> Bool, _ name: String) {
    guard condition() else { fatalError("FAIL: \(name)") }
    checks += 1; print("PASS: \(name)")
}
let dir = FileManager.default.temporaryDirectory.appendingPathComponent("ClipV-tests-\(UUID().uuidString)")
defer { try? FileManager.default.removeItem(at: dir) }
let file = dir.appendingPathComponent("history.json")
let h = History(limit: 3, file: file)
check(!h.persistent, "persistence defaults off")
check(!h.capture(nil, types: []), "nil ignored")
check(!h.capture("", types: []), "empty ignored")
check(h.capture("alpha", types: []), "capture text")
check(!h.capture("alpha", types: []), "consecutive duplicate ignored")
for type in History.excludedTypes { check(!h.capture("synthetic-secret", types: [type, "public.utf8-plain-text"]), "sensitive marker rejected: \(type)") }
h.paused = true
check(!h.capture("paused", types: []), "pause ignores capture")
h.paused = false
check(h.capture("https://example.com/clipv", types: []), "URL captured")
check(h.capture("Ação\nMultiline", types: []), "multiline Unicode captured")
check(h.search("ACAO").count == 1, "case and accent insensitive search")
check(h.items.first?.text == "Ação\nMultiline", "newest first")
_ = h.capture("fourth", types: [])
check(h.items.count == 3 && !h.items.contains { $0.text == "alpha" }, "limit prunes oldest")
try h.save()
check(!FileManager.default.fileExists(atPath: file.path), "disabled persistence creates no file")
h.persistent = true
try h.save()
let restored = History(limit: 3, file: file); restored.persistent = true
try restored.load()
check(restored.items == h.items, "persistence round trip")
let attrs = try FileManager.default.attributesOfItem(atPath: file.path)
let directoryAttrs = try FileManager.default.attributesOfItem(atPath: dir.path)
check((directoryAttrs[.posixPermissions] as? NSNumber)?.intValue == 0o700, "private directory permissions")
check((attrs[.posixPermissions] as? NSNumber)?.intValue == 0o600, "private file permissions")
h.limit = 1
check(h.items.count == 1, "reducing limit trims")
h.remove(h.items[0].id)
check(h.items.isEmpty, "delete selected")
_ = h.capture("clear-me", types: []); h.clear(); try h.save()
try restored.load()
check(restored.items.isEmpty, "clear persists empty history")
try h.deleteSaved()
check(!FileManager.default.fileExists(atPath: file.path), "disabling persistence deletes saved file")
check(!h.capture(String(repeating: "a", count: 2_000_001), types: []), "oversized text ignored")
let bounded = History(limit: 200, file: file)
for i in 0..<220 { _ = bounded.capture("item-\(i)", types: []) }
check(bounded.items.count == 200 && bounded.items.last?.text == "item-20", "default 200 item cap")
let payload = History(limit: 200, file: file)
for i in 0..<20 { _ = payload.capture(String(repeating: "x", count: 1_500_000) + String(i), types: []) }
check(payload.items.reduce(0) { $0 + $1.text.utf8.count } <= 24_000_000, "total text payload bounded")
check(payload.items.first?.text.hasSuffix("19") == true, "payload trimming keeps newest")
try FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
try Data("not-json".utf8).write(to: file)
let malformed = History(file: file); malformed.persistent = true
var rejected = false
do { try malformed.load() } catch { rejected = true }
check(rejected && malformed.items.isEmpty, "malformed saved data rejected safely")
check(FileManager.default.fileExists(atPath: file.path), "failed load preserves source file")
let favorites = History(limit: 3, file: file)
_ = favorites.capture("favorite", types: [])
let favoriteID = favorites.items[0].id; favorites.togglePin(favoriteID)
_ = favorites.capture("old transient", types: [])
_ = favorites.capture("middle transient", types: [])
_ = favorites.capture("new transient", types: [])
check(favorites.items.contains { $0.id == favoriteID && $0.isPinned }, "pinned item protected from eviction")
check(!favorites.items.contains { $0.text == "old transient" }, "oldest unpinned item evicted first")
check(favorites.search("", scope: 1).count == 1, "pinned scope")
favorites.clearUnpinned()
check(favorites.items.count == 1 && favorites.items[0].isPinned, "clear recents preserves pins")
favorites.persistent = true; try favorites.save()
let favoriteRestored = History(file: file); favoriteRestored.persistent = true; try favoriteRestored.load()
check(favoriteRestored.items.first?.isPinned == true, "pins persisted when storage enabled")
favorites.togglePin(favoriteID)
check(!favorites.items[0].isPinned, "unpin item")
let types = History(file: file)
_ = types.capture("  https://example.com/test ", types: [])
_ = types.capture("line one\n\nline two\tthree", types: [])
check(types.search("", scope: 2).count == 1, "link filter trims surrounding whitespace")
check(types.search("", scope: 3).count == 1, "text-only filter")
check(types.search("example", scope: 3).isEmpty, "query and scope compose")
check(types.items[0].singleLine == "line one line two three", "copy as one line normalizes whitespace")
let fullPins = History(limit: 1, file: file)
_ = fullPins.capture("pinned capacity", types: []); fullPins.togglePin(fullPins.items[0].id)
check(!fullPins.capture("incoming", types: []), "full pinned capacity rejects incoming without false success")
check(fullPins.items[0].text == "pinned capacity", "pin survives full capacity capture")
let legacy = [["id": UUID().uuidString, "text": "legacy format", "date": 0] as [String: Any]]
try JSONSerialization.data(withJSONObject: legacy).write(to: file)
let legacyModel = History(file: file); legacyModel.persistent = true; try legacyModel.load()
check(legacyModel.items.count == 1 && !legacyModel.items[0].isPinned, "older saved history loads without pinned field")
print("\(checks) checks passed; synthetic data only")

// Raster conversion and clipboard round trips use a private pasteboard, never the user's.
import AppKit
import UniformTypeIdentifiers
import ImageIO
let pixels: [UInt8] = [255, 0, 0, 255, 0, 255, 0, 128, 0, 0, 255, 0, 255, 255, 255, 255]
let provider = CGDataProvider(data: Data(pixels) as CFData)!
let cg = CGImage(width: 2, height: 2, bitsPerComponent: 8, bitsPerPixel: 32, bytesPerRow: 8, space: CGColorSpaceCreateDeviceRGB(), bitmapInfo: CGBitmapInfo(rawValue: CGImageAlphaInfo.last.rawValue), provider: provider, decode: nil, shouldInterpolate: false, intent: .defaultIntent)!
let png = ImageClipboard.encode(cg, type: .png)!
let image = ImageClipboard.normalize(png)!
check(image.width == 2 && image.height == 2, "PNG normalized without changing raster size")
check(ImageClipboard.thumbnail(image) != nil, "thumbnail decodes")
check(ImageClipboard.normalize(Data([0, 1, 2, 3])) == nil, "invalid image rejected")
check(ImageClipboard.normalize(Data(repeating: 0, count: ImageClipboard.maxInputBytes + 1)) == nil, "oversized input rejected")
let images = History(limit: 4, file: dir.appendingPathComponent("images.json"))
check(images.captureImage(image, types: []), "image captured")
check(!images.captureImage(image, types: []), "consecutive duplicate image ignored")
check(images.items[0].isImage && !images.items[0].isLink, "image typed separately from text and links")
check(images.search("", scope: 4).count == 1 && images.search("", scope: 3).isEmpty, "image and text filters separated")
check(images.search("IMAGEM").count == 1, "image label searchable without OCR")
check(!images.captureImage(StoredImage(png: Data([1]), width: 8192, height: 8192), types: []), "pixel limit enforced")
check(!images.captureImage(StoredImage(png: Data(repeating: 0, count: 8_000_001), width: 2, height: 2), types: []), "encoded image limit enforced")
for type in History.excludedTypes { check(!images.captureImage(image, types: [type]), "image sensitive marker rejected: \(type)") }
images.paused = true; check(!images.captureImage(image, types: []), "pause blocks images"); images.paused = false
_ = images.capture("between images", types: [])
check(images.captureImage(image, types: []), "same image after another item is retained")
images.togglePin(images.items[0].id); images.persistent = true; try images.save()
let loadedImages = History(file: images.file); loadedImages.persistent = true; try loadedImages.load()
check(loadedImages.items == images.items, "mixed text image persistence round trip with pin")
let pb = NSPasteboard.withUniqueName(); defer { pb.releaseGlobally() }
check(ImageClipboard.write(Clip(image: image), to: pb), "image written to native pasteboard")
check(pb.types?.contains(.png) == true && pb.types?.contains(.tiff) == true && pb.string(forType: .string) == nil, "PNG and TIFF offered without text placeholder")
check(ImageClipboard.normalize(ImageClipboard.offeredData(on: pb)!) == image, "clipboard raster round trip retains normalized PNG")
let tiff = pb.data(forType: .tiff)!
pb.clearContents(); pb.setData(tiff, forType: .tiff)
check(ImageClipboard.normalize(ImageClipboard.offeredData(on: pb)!)?.width == 2, "TIFF only clipboard supported")
let jpeg = ImageClipboard.encode(cg, type: .jpeg)!
pb.clearContents(); pb.setData(jpeg, forType: NSPasteboard.PasteboardType(UTType.jpeg.identifier))
check(ImageClipboard.normalize(ImageClipboard.offeredData(on: pb)!)?.height == 2, "JPEG only clipboard supported")
pb.clearContents(); pb.setString("file:///tmp/not-read.png", forType: .fileURL)
check(ImageClipboard.offeredData(on: pb) == nil, "file URL does not read file from disk")
pb.clearContents(); pb.setString("preserved", forType: .string)
check(!ImageClipboard.write(Clip(image: image), to: pb, singleLine: true) && pb.string(forType: .string) == "preserved", "text transform cannot overwrite image or clipboard")
let budget = History(file: file)
for n in 0..<4 { _ = budget.captureImage(StoredImage(png: Data(repeating: UInt8(n), count: 7_000_000), width: 10, height: 10), types: []) }
check(budget.items.count == 3 && budget.items.reduce(0) { $0 + $1.byteCount } <= 24_000_000, "aggregate budget counts images")
let alphaBitmap = NSBitmapImageRep(data: image.png)!
check(abs((alphaBitmap.colorAt(x: 1, y: 0)?.alphaComponent ?? 0) - 128.0 / 255.0) < 0.02, "PNG preserves partial alpha transparency")
let wide = cg.cropping(to: CGRect(x: 0, y: 0, width: 2, height: 1))!
let rotatedJPEG = ImageClipboard.encode(wide, type: .jpeg, properties: [kCGImagePropertyOrientation: 6])!
let oriented = ImageClipboard.normalize(rotatedJPEG)!
check(oriented.width == 1 && oriented.height == 2, "EXIF orientation applied before storing PNG")
print("\(checks) total checks passed; synthetic data and private clipboard only")
