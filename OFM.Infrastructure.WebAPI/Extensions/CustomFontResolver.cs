using PdfSharp.Fonts;

namespace OFM.Infrastructure.WebAPI.Extensions
{
    public class CustomFontResolver : IFontResolver
    {
        private readonly Dictionary<string, byte[]> _fontData = new();

        public CustomFontResolver()
        {
            // Load font data
            _fontData["Calibri#Regular"] = File.ReadAllBytes("Fonts/Calibri-Regular.ttf");
            _fontData["Calibri#Bold"] = File.ReadAllBytes("Fonts/Calibri-Bold.ttf");
        }

        public byte[] GetFont(string faceName)
        {
            if (_fontData.TryGetValue(faceName, out var data))
                return data;

            throw new InvalidOperationException($"Font '{faceName}' not found.");
        }

        public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
        {
            if (familyName.Equals("Calibri", StringComparison.OrdinalIgnoreCase))
            {
                if (isBold)
                    return new FontResolverInfo("Calibri#Bold");
                else
                    return new FontResolverInfo("Calibri#Regular");
            }
            // Fallback to default
            return PlatformFontResolver.ResolveTypeface(familyName, isBold, isItalic);
        }
    }
}
