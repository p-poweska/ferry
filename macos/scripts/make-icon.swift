#!/usr/bin/env swift
// Draws the Ferry app icon. Writes Resources/AppIcon.icns for the Mac app and
// the shared ../Resources/AppIcon.png.
// Usage (from macos/): swift scripts/make-icon.swift
import AppKit

let canvas: CGFloat = 1024

func drawIcon(in context: CGContext) {
    // Apple's icon grid: an 824pt rounded square centred on a 1024pt canvas.
    let body = CGRect(x: 100, y: 100, width: 824, height: 824)
    let shape = NSBezierPath(roundedRect: body, xRadius: 185, yRadius: 185)

    NSGraphicsContext.saveGraphicsState()
    let shadow = NSShadow()
    shadow.shadowColor = NSColor.black.withAlphaComponent(0.3)
    shadow.shadowBlurRadius = 24
    shadow.shadowOffset = NSSize(width: 0, height: -10)
    shadow.set()
    NSColor.black.setFill()
    shape.fill()
    NSGraphicsContext.restoreGraphicsState()

    NSGraphicsContext.saveGraphicsState()
    shape.addClip()

    // Sky to deep water.
    NSGradient(colors: [
        NSColor(srgbRed: 0.42, green: 0.74, blue: 1.00, alpha: 1),
        NSColor(srgbRed: 0.20, green: 0.47, blue: 0.96, alpha: 1),
        NSColor(srgbRed: 0.10, green: 0.25, blue: 0.72, alpha: 1),
    ])!.draw(in: shape, angle: -90)

    // Soft waves along the bottom.
    for (index, height) in [CGFloat(300), 200].enumerated() {
        let wave = NSBezierPath()
        wave.move(to: NSPoint(x: body.minX, y: body.minY))
        wave.line(to: NSPoint(x: body.minX, y: body.minY + height))
        let crest: CGFloat = 26
        let step = body.width / 4
        for i in 0..<4 {
            let x = body.minX + CGFloat(i) * step
            let up = (i + index) % 2 == 0
            wave.curve(
                to: NSPoint(x: x + step, y: body.minY + height),
                controlPoint1: NSPoint(x: x + step / 3, y: body.minY + height + (up ? crest : -crest)),
                controlPoint2: NSPoint(x: x + step * 2 / 3, y: body.minY + height + (up ? crest : -crest))
            )
        }
        wave.line(to: NSPoint(x: body.maxX, y: body.minY))
        wave.close()
        NSColor(srgbRed: 0.04, green: 0.14, blue: 0.50, alpha: 0.22 + CGFloat(index) * 0.12).setFill()
        wave.fill()
    }

    // The ferry itself.
    let config = NSImage.SymbolConfiguration(pointSize: 400, weight: .semibold)
        .applying(.init(paletteColors: [.white]))
    if let symbol = NSImage(systemSymbolName: "ferry.fill", accessibilityDescription: nil)?
        .withSymbolConfiguration(config) {
        let size = symbol.size
        let rect = NSRect(
            x: (canvas - size.width) / 2,
            y: (canvas - size.height) / 2 + 40,
            width: size.width,
            height: size.height
        )
        let symbolShadow = NSShadow()
        symbolShadow.shadowColor = NSColor(srgbRed: 0.05, green: 0.15, blue: 0.45, alpha: 0.45)
        symbolShadow.shadowBlurRadius = 30
        symbolShadow.shadowOffset = NSSize(width: 0, height: -12)
        symbolShadow.set()
        symbol.draw(in: rect)
    }
    NSGraphicsContext.restoreGraphicsState()

    // Subtle rim light.
    NSColor.white.withAlphaComponent(0.18).setStroke()
    let rim = NSBezierPath(roundedRect: body.insetBy(dx: 2, dy: 2), xRadius: 183, yRadius: 183)
    rim.lineWidth = 4
    rim.stroke()
}

func png(pixels: Int) -> Data {
    let rep = NSBitmapImageRep(
        bitmapDataPlanes: nil, pixelsWide: pixels, pixelsHigh: pixels,
        bitsPerSample: 8, samplesPerPixel: 4, hasAlpha: true, isPlanar: false,
        colorSpaceName: .deviceRGB, bytesPerRow: 0, bitsPerPixel: 0
    )!
    rep.size = NSSize(width: canvas, height: canvas)
    NSGraphicsContext.saveGraphicsState()
    let context = NSGraphicsContext(bitmapImageRep: rep)!
    NSGraphicsContext.current = context
    context.imageInterpolation = .high
    drawIcon(in: context.cgContext)
    NSGraphicsContext.restoreGraphicsState()
    return rep.representation(using: .png, properties: [:])!
}

let root = URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent()
let iconset = FileManager.default.temporaryDirectory.appendingPathComponent("AppIcon.iconset")
try? FileManager.default.removeItem(at: iconset)
try FileManager.default.createDirectory(at: iconset, withIntermediateDirectories: true)

for points in [16, 32, 128, 256, 512] {
    try png(pixels: points).write(to: iconset.appendingPathComponent("icon_\(points)x\(points).png"))
    try png(pixels: points * 2).write(to: iconset.appendingPathComponent("icon_\(points)x\(points)@2x.png"))
}

let output = root.appendingPathComponent("Resources/AppIcon.icns")
let iconutil = Process()
iconutil.executableURL = URL(fileURLWithPath: "/usr/bin/iconutil")
iconutil.arguments = ["-c", "icns", iconset.path, "-o", output.path]
try iconutil.run()
iconutil.waitUntilExit()
try png(pixels: 512).write(to: root.appendingPathComponent("../Resources/AppIcon.png").standardizedFileURL)
print("Wrote \(output.path)")
