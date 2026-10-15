using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DBTool.Core
{
    /// <summary>
    /// Lưu vết nguồn dữ liệu chính xác từ ô Excel (Provenance Tracking)
    /// </summary>
    public record SourceRef(string FilePath, string SheetName, string CellAddress, string RawValue)
    {
        public override string ToString() => $"[{SheetName}!{CellAddress}='{RawValue}']";
    }

    /// <summary>
    /// Mức độ cảnh báo / lỗi chẩn đoán
    /// </summary>
    public enum DiagnosticSeverity
    {
        Info,
        Review,
        Block
    }

    /// <summary>
    /// Thông điệp chẩn đoán có địa chỉ ô Excel
    /// </summary>
    public record Diagnostic(
        DiagnosticSeverity Severity,
        string ManholeName,
        SourceRef? Source,
        string Message,
        string? Condition = null)
    {
        public override string ToString() =>
            $"[{Severity}] Ga '{ManholeName}' {Source}: {Message} {(Condition != null ? "(" + Condition + ")" : "")}";
    }

    /// <summary>
    /// Loại hố ga được hỗ trợ
    /// </summary>
    public enum ManholeType
    {
        GaThamLienKhoi,      // Loại 1: Ga thăm BTCT liền khối
        GaThamNapRoi,        // Loại 2: Ga thăm BTCT nắp rời (tấm đan trần đúc sẵn)
        GaThamThuNapRoi     // Loại 3: Ga thăm + thu BTCT nắp rời (có cửa thu nước)
    }

    /// <summary>
    /// Hướng tường hố ga chuẩn theo PDF tr. 7, 10
    /// </summary>
    public enum WallDirection
    {
        T1 = 1, // 12h (Phía trên, +Y)
        T2 = 2, // 3h  (Bên phải, +X)
        T3 = 3, // 6h  (Phía dưới, -Y)
        T4 = 4  // 9h  (Bên trái, -X)
    }

    /// <summary>
    /// Cốt thép đã được chuẩn hóa
    /// </summary>
    public record RebarSpec
    {
        public int Diameter { get; init; }
        public double Spacing { get; init; }
        public int Count { get; init; }
        public double Length { get; init; }
        public string Prefix { get; init; } = "";
        public string RawText { get; init; } = "";
        public SourceRef? Source { get; init; }

        private static readonly Regex RebarPattern = new Regex(
            @"^(?:(?<count>\d+))?(?<prefix>[a-zA-Z\u00C0-\u024F\u1EA0-\u1EF9]+)?(?<type>[D|Phi|\u03A6|d])(?<dia>\d+)(?:[aA/](?<spacing>\d+))?(?:[lL](?<len>\d+))?$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static RebarSpec? Parse(string? text, SourceRef? source = null, List<Diagnostic>? diagnostics = null, string manholeName = "")
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            string clean = text.Trim().Replace(" ", "");

            var m = RebarPattern.Match(clean);
            if (!m.Success)
            {
                diagnostics?.Add(new Diagnostic(
                    DiagnosticSeverity.Block,
                    manholeName,
                    source,
                    $"Chuỗi cốt thép không đúng định dạng: '{text}'. Ví dụ chuẩn: 'D10A150', '2D12', 'D6A150L100'."));
                return null;
            }

            int count = 1;
            if (m.Groups["count"].Success) int.TryParse(m.Groups["count"].Value, out count);

            int.TryParse(m.Groups["dia"].Value, out int dia);

            double spacing = 0;
            if (m.Groups["spacing"].Success) double.TryParse(m.Groups["spacing"].Value, out spacing);

            double len = 0;
            if (m.Groups["len"].Success) double.TryParse(m.Groups["len"].Value, out len);

            string prefix = m.Groups["prefix"].Success ? m.Groups["prefix"].Value : "";

            return new RebarSpec
            {
                Diameter = dia,
                Spacing = spacing,
                Count = count,
                Length = len,
                Prefix = prefix,
                RawText = text,
                Source = source
            };
        }
    }

    /// <summary>
    /// Thông số tra cứu cống từ sheet CF.Cong
    /// </summary>
    public record PipeRecord(
        string Code,
        bool IsBox,
        double InnerDia,      // D cống tròn
        double WallThickness, // tp
        double OuterDia,      // D + 2*tp
        double BoxInnerWidth,
        double BoxInnerHeight,
        double BoxChamfer,
        bool IsDouble);

    /// <summary>
    /// Thông số tra cứu thép góc từ sheet CF.Data
    /// </summary>
    public record SteelAngleRecord(
        string Code,
        double WeightKgPerMeter);

    /// <summary>
    /// Thông số mẫu hatch và tỷ lệ từ sheet CF.Data
    /// </summary>
    public record HatchRecord(
        string PatternName,
        double Scale);
}
