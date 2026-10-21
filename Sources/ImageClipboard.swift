import AppKit
import ImageIO
import UniformTypeIdentifiers

/// Only raster image bytes explicitly offered by the clipboard are read.
/// File URLs are never resolved and remote images are never downloaded.
enum ImageClipboard {
    static let maxInputBytes = 32_000_000
    static func source(_ data: Data) -> CGImageSource? {
        guard !data.isEmpty, data.count <= maxInputBytes,
              let source = CGImageSourceCreateWithData(data as CFData, [kCGImageSourceShouldCache: false] as CFDictionary),
              let properties = CGImageSourceCopyPropertiesAtIndex(source, 0, nil) as? [CFString: Any],
              let width = properties[kCGImagePropertyPixelWidth] as? Int,
              let height = properties[kCGImagePropertyPixelHeight] as? Int,
              width > 0, height > 0, width <= 8192, height <= 8192,
              width * height <= 16_000_000 else { return nil }
        return source
    }
    static func encode(_ image: CGImage, type: UTType, properties: [CFString: Any] = [:]) -> Data? {
        let result = NSMutableData()
        guard let destination = CGImageDestinationCreateWithData(result, type.identifier as CFString, 1, nil) else { return nil }
        CGImageDestinationAddImage(destination, image, properties as CFDictionary)
        guard CGImageDestinationFinalize(destination) else { return nil }
        return result as Data
    }
    static func normalize(_ data: Data) -> StoredImage? {
        guard let source = source(data),
              // Apply EXIF orientation, retain the original raster resolution, use the first frame.
              let image = CGImageSourceCreateThumbnailAtIndex(source, 0, [
                kCGImageSourceCreateThumbnailFromImageAlways: true,
                kCGImageSourceCreateThumbnailWithTransform: true,
                kCGImageSourceThumbnailMaxPixelSize: 8192,
                kCGImageSourceShouldCacheImmediately: true
              ] as CFDictionary), let png = encode(image, type: .png) else { return nil }
        let stored = StoredImage(png: png, width: image.width, height: image.height)
        return stored.isWithinLimits ? stored : nil
    }
    static func thumbnail(_ image: StoredImage, maxSize: Int = 256) -> NSImage? {
        guard image.isWithinLimits, let source = source(image.png),
              let cg = CGImageSourceCreateThumbnailAtIndex(source, 0, [
                kCGImageSourceCreateThumbnailFromImageAlways: true,
                kCGImageSourceThumbnailMaxPixelSize: maxSize,
                kCGImageSourceCreateThumbnailWithTransform: true
              ] as CFDictionary) else { return nil }
        return NSImage(cgImage: cg, size: NSSize(width: cg.width, height: cg.height))
    }
    static func offeredData(on pb: NSPasteboard) -> Data? {
        let types = pb.types ?? []
        let candidates: [NSPasteboard.PasteboardType] = [.png, .tiff] + types.filter {
            guard $0 != .png && $0 != .tiff, let type = UTType($0.rawValue) else { return false }
            return type.conforms(to: .image) && type != .pdf && type != .svg
        }
        for type in candidates where types.contains(type) {
            if let data = pb.data(forType: type), !data.isEmpty, data.count <= maxInputBytes { return data }
        }
        return nil
    }
    @discardableResult static func write(_ clip: Clip, to pb: NSPasteboard, singleLine: Bool = false) -> Bool {
        if let image = clip.image {
            guard !singleLine, image.isWithinLimits, let source = source(image.png),
                  let cg = CGImageSourceCreateImageAtIndex(source, 0, nil) else { return false }
            let item = NSPasteboardItem(); item.setData(image.png, forType: .png)
            // TIFF supports native editors that do not request PNG from the pasteboard.
            if let tiff = encode(cg, type: .tiff, properties: [kCGImagePropertyTIFFDictionary: [kCGImagePropertyTIFFCompression: 5]]) { item.setData(tiff, forType: .tiff) }
            pb.clearContents(); return pb.writeObjects([item])
        }
        pb.clearContents(); return pb.setString(singleLine ? clip.singleLine : clip.text, forType: .string)
    }
}
