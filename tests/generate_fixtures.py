from pathlib import Path
import struct
import zlib
from PIL import Image, ImageCms, PngImagePlugin

directory = Path(__file__).parent / "AlphaTrimmer.Tests" / "Fixtures"
directory.mkdir(exist_ok=True)
image = Image.new("RGBA", (10, 10), (255, 0, 0, 0))
for y in range(4, 6):
    for x in range(3, 6):
        image.putpixel((x, y), (13 + x, 29 + y, 97, 1 if x == 3 else 128))
image.putpixel((4, 4), (123, 234, 45, 0))
image.save(directory / "rgba.png")
image.save(directory / "rgba.webp", lossless=True, exact=True)
image.save(directory / "lossy.webp", quality=70)
transparent = Image.new("RGBA", (10, 10), (255, 0, 0, 0))
transparent.save(directory / "transparent.png")
transparent.save(directory / "transparent.webp", lossless=True, exact=True)
Image.new("RGBA", (10, 10), (5, 6, 7, 128)).save(directory / "no-margin.png")
Image.new("RGB", (10, 10), (5, 6, 7)).save(directory / "opaque.png")
rgb_transparency = Image.new("RGB", (10, 10), (255, 0, 0))
rgb_transparency.putpixel((2, 3), (13, 29, 97))
rgb_transparency.save(directory / "rgb-transparency.png", transparency=(255, 0, 0))
palette = Image.new("P", (10, 10), 0)
palette.putpalette([255, 0, 0, 0, 255, 0] + [0] * 762)
palette.putpixel((2, 3), 1)
palette.save(directory / "palette.png", transparency=bytes([0, 1]))
gray = Image.new("LA", (10, 10), (255, 0))
gray.putpixel((2, 3), (123, 1))
gray.save(directory / "gray-alpha.png")
exif = Image.Exif()
exif[315] = "Tatsuya"
exif[34665] = {36867: "2025:04:09 12:34:56", 40962: 10, 40963: 10}
icc = ImageCms.ImageCmsProfile(ImageCms.createProfile("sRGB")).tobytes()
text = PngImagePlugin.PngInfo()
text.add_itxt("Description", "透明画像のテスト")
xmp = b'<x:xmpmeta xmlns:x="adobe:ns:meta/"><rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#"><rdf:Description xmlns:tiff="http://ns.adobe.com/tiff/1.0/" xmlns:exif="http://ns.adobe.com/exif/1.0/" xmlns:dc="http://purl.org/dc/elements/1.1/" tiff:ImageWidth="10" exif:PixelYDimension="10"><dc:creator>Tatsuya</dc:creator></rdf:Description></rdf:RDF></x:xmpmeta>'
text.add_itxt("XML:com.adobe.xmp", xmp.decode())
image.save(directory / "metadata.png", exif=exif, icc_profile=icc, pnginfo=text, dpi=(300, 300))
image.save(directory / "metadata.webp", exif=exif, icc_profile=icc, xmp=xmp, lossless=True, exact=True)
other = image.copy()
other.putpixel((8, 8), (0, 255, 0, 255))
image.save(directory / "animated.png", save_all=True, append_images=[other], duration=100, loop=0)
image.save(directory / "animated.webp", save_all=True, append_images=[other], duration=100, loop=0, lossless=True)
(directory / "broken.png").write_bytes(b"\x89PNG\r\n\x1a\n" + b"broken image")

def chunk(name, data):
    return struct.pack(">I", len(data)) + name + data + struct.pack(">I", zlib.crc32(name + data))

raw = b""
for y in range(10):
    raw += b"\0"
    for x in range(10):
        pixel = (123, 10001, 50003, 1 if x == 2 else 32769) if y == 3 and x in (2, 3) else (500, 1000, 2000, 0)
        raw += struct.pack(">4H", *pixel)
png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", 10, 10, 16, 6, 0, 0, 0))
png += chunk(b"IDAT", zlib.compress(raw)) + chunk(b"IEND", b"")
(directory / "rgba16.png").write_bytes(png)
