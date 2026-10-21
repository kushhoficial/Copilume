import AppKit
@main struct MakeIcon {
    static func main() throws {
        guard CommandLine.arguments.count == 4 else { fatalError("Usage: MakeIcon ICONSET MASTER_PNG RESOURCES") }
        let directory = URL(fileURLWithPath: CommandLine.arguments[1], isDirectory: true)
        let master = URL(fileURLWithPath: CommandLine.arguments[2])
        let resources = URL(fileURLWithPath: CommandLine.arguments[3], isDirectory: true)
        guard let source = NSImage(contentsOf: master) else { fatalError("Cannot read official icon") }
        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        try FileManager.default.createDirectory(at: resources, withIntermediateDirectories: true)
        for points in [16, 32, 128, 256, 512] {
            for scale in [1, 2] {
                let pixels = points * scale
                let image = NSImage(size: NSSize(width: pixels, height: pixels), flipped: false) { rect in
                    NSGraphicsContext.current?.imageInterpolation = .high
                    source.draw(in: rect, from: .zero, operation: .sourceOver, fraction: 1); return true
                }
                let cg = image.cgImage(forProposedRect: nil, context: nil, hints: nil)!
                let bitmap = NSBitmapImageRep(cgImage: cg)
                let filename = "icon_\(points)x\(points)\(scale == 2 ? "@2x" : "").png"
                try bitmap.representation(using: .png, properties: [:])!.write(to: directory.appendingPathComponent(filename))
            }
        }
        let glyph = MenuBrandGlyph(frame: NSRect(x: 0, y: 0, width: 24, height: 24))
        let pdf = glyph.dataWithPDF(inside: glyph.bounds)
        try pdf.write(to: resources.appendingPathComponent("MenuIconTemplate.pdf"))
        guard let template = NSImage(data: pdf) else { fatalError("Invalid menu PDF") }
        for pixels in [16, 18, 22, 32, 36, 44] {
            let image = NSImage(size: NSSize(width: pixels, height: pixels), flipped: false) { rect in
                template.draw(in: rect, from: .zero, operation: .sourceOver, fraction: 1); return true
            }
            let rep = NSBitmapImageRep(cgImage: image.cgImage(forProposedRect: nil, context: nil, hints: nil)!)
            try rep.representation(using: .png, properties: [:])!.write(to: directory.appendingPathComponent("menu_\(pixels).png"))
        }
    }
}
