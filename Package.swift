// swift-tools-version:6.0
import PackageDescription

let package = Package(
    name: "Ferry",
    platforms: [.macOS(.v14)],
    targets: [
        .executableTarget(
            name: "Ferry",
            path: "Sources/Ferry"
        ),
        .testTarget(
            name: "FerryTests",
            dependencies: ["Ferry"],
            path: "Tests/FerryTests"
        ),
    ]
)
