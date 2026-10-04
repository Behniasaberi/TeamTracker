using System.IO.Compression;
using System.Security;
using System.Text;

namespace TeamTracker.Services;

public class XlsxWriter
{
    public class Sheet
    {
        public string Name { get; init; } = "Sheet";
        public string[] Headers { get; init; } = Array.Empty<string>();
        public double[] Widths { get; init; } = Array.Empty<double>();
        public List<object?[]> Rows { get; } = new();
    }

    private readonly List<Sheet> _sheets = new();

    public Sheet AddSheet(string name, string[] headers, double[]? widths = null)
    {
        var s = new Sheet { Name = SafeName(name), Headers = headers, Widths = widths ?? headers.Select(_ => 16.0).ToArray() };
        _sheets.Add(s);
        return s;
    }

    public byte[] ToBytes()
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(zip, "[Content_Types].xml", ContentTypes());
            Write(zip, "_rels/.rels",
                """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""");
            Write(zip, "xl/workbook.xml", Workbook());
            Write(zip, "xl/_rels/workbook.xml.rels", WorkbookRels());
            Write(zip, "xl/styles.xml", Styles());
            for (var i = 0; i < _sheets.Count; i++)
                Write(zip, $"xl/worksheets/sheet{i + 1}.xml", SheetXml(_sheets[i]));
        }
        return ms.ToArray();
    }

    private static void Write(ZipArchive zip, string path, string content)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        using var w = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        w.Write(content);
    }

    private string ContentTypes()
    {
        var sb = new StringBuilder("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>""");
        for (var i = 1; i <= _sheets.Count; i++)
            sb.Append($"""<Override PartName="/xl/worksheets/sheet{i}.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>""");
        return sb.Append("</Types>").ToString();
    }

    private string Workbook()
    {
        var sb = new StringBuilder("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><bookViews><workbookView/></bookViews><sheets>""");
        for (var i = 0; i < _sheets.Count; i++)
            sb.Append($"""<sheet name="{Esc(_sheets[i].Name)}" sheetId="{i + 1}" r:id="rId{i + 1}"/>""");
        return sb.Append("</sheets></workbook>").ToString();
    }

    private string WorkbookRels()
    {
        var sb = new StringBuilder("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">""");
        for (var i = 0; i < _sheets.Count; i++)
            sb.Append($"""<Relationship Id="rId{i + 1}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet{i + 1}.xml"/>""");
        sb.Append($"""<Relationship Id="rId{_sheets.Count + 1}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>""");
        return sb.Append("</Relationships>").ToString();
    }

    // استایل ۰: معمولی · ۱: عنوان (پررنگ، پس‌زمینه) · ۲: عدد با دو رقم اعشار
    private static string Styles() =>
        """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><numFmts count="1"><numFmt numFmtId="164" formatCode="0.##"/></numFmts><fonts count="2"><font><sz val="11"/><name val="Tahoma"/></font><font><b/><sz val="11"/><color rgb="FFFFFFFF"/><name val="Tahoma"/></font></fonts><fills count="3"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill><fill><patternFill patternType="solid"><fgColor rgb="FF5B4CF0"/><bgColor indexed="64"/></patternFill></fill></fills><borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders><cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs><cellXfs count="3"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="1" fillId="2" borderId="0" xfId="0" applyFont="1" applyFill="1"/><xf numFmtId="164" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/></cellXfs><cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles></styleSheet>""";

    private static string SheetXml(Sheet s)
    {
        var sb = new StringBuilder("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetViews><sheetView rightToLeft="1" workbookViewId="0"><pane ySplit="1" topLeftCell="A2" activePane="bottomLeft" state="frozen"/></sheetView></sheetViews><cols>""");
        for (var c = 0; c < s.Widths.Length; c++)
            sb.Append($"""<col min="{c + 1}" max="{c + 1}" width="{s.Widths[c].ToString(System.Globalization.CultureInfo.InvariantCulture)}" customWidth="1"/>""");
        sb.Append("</cols><sheetData>");

        AppendRow(sb, 1, s.Headers.Cast<object?>().ToArray(), header: true);
        for (var r = 0; r < s.Rows.Count; r++) AppendRow(sb, r + 2, s.Rows[r], header: false);

        sb.Append("</sheetData>");
        if (s.Rows.Count > 0)
            sb.Append($"""<autoFilter ref="A1:{Col(s.Headers.Length - 1)}{s.Rows.Count + 1}"/>""");
        return sb.Append("</worksheet>").ToString();
    }

    private static void AppendRow(StringBuilder sb, int rowNum, object?[] cells, bool header)
    {
        sb.Append($"""<row r="{rowNum}">""");
        for (var c = 0; c < cells.Length; c++)
        {
            var refName = $"{Col(c)}{rowNum}";
            switch (cells[c])
            {
                case null:
                    break;
                case double or int or long or decimal when !header:
                    var num = Convert.ToDouble(cells[c]).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    sb.Append($"""<c r="{refName}" s="2"><v>{num}</v></c>""");
                    break;
                default:
                    sb.Append($"""<c r="{refName}" t="inlineStr"{(header ? " s=\"1\"" : "")}><is><t xml:space="preserve">{Esc(cells[c]!.ToString()!)}</t></is></c>""");
                    break;
            }
        }
        sb.Append("</row>");
    }

    private static string Col(int index)
    {
        var name = "";
        for (index++; index > 0; index = (index - 1) / 26)
            name = (char)('A' + (index - 1) % 26) + name;
        return name;
    }

    private static string Esc(string s) => SecurityElement.Escape(s) ?? "";

    private static string SafeName(string name)
    {
        foreach (var ch in "[]:*?/\\") name = name.Replace(ch, '-');
        return name.Length > 31 ? name[..31] : name;
    }
}
