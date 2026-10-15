using System;
using System.Collections.Generic;
using System.Linq;

namespace DBTool.Core
{
    /// <summary>
    /// Một dòng chi tiết thống kê cốt thép hoàn chỉnh (đối tượng chung duy nhất cho CAD và Excel)
    /// </summary>
    public class BarScheduleItem
    {
        public string ComponentGroup { get; init; } = "";
        public string BarMark { get; init; } = "";
        public int Diameter { get; init; }
        public string AngleCode { get; init; } = "";
        public bool IsAngleSteel => !string.IsNullOrEmpty(AngleCode);
        public BarShapeType Shape { get; init; }
        public int Quantity { get; init; }
        public double LengthMm { get; init; }
        public double TotalLengthM { get; init; }
        public double UnitWeightKgM { get; init; }
        public double TotalWeightKg { get; init; }

        // Các đoạn kích thước hình học uốn (mm)
        public double L { get; init; }
        public double LT { get; init; }
        public double LP { get; init; }
        public double L1T { get; init; }
        public double L2T { get; init; }
        public double L3 { get; init; }
        public double L1P { get; init; }
        public double L2P { get; init; }

        public string Note { get; init; } = "";
    }

    /// <summary>
    /// Bảng tổng hợp khối lượng cốt thép phân nhóm theo đường kính (D <= 10 và D > 10)
    /// Khớp 100% với các Tag ATTDEF của block TK.THKL, TK.THKLNAP, TK.THKL.CT
    /// </summary>
    public class SteelSummary
    {
        // Bản đáy
        public double Day1 { get; set; } // D <= 10
        public double Day2 { get; set; } // D > 10

        // Tường
        public double Tuong1 { get; set; } // D <= 10
        public double Tuong2 { get; set; } // D > 10

        // Bản trần
        public double Tran1 { get; set; } // D <= 10
        public double Tran2 { get; set; } // D > 10

        // Cổ ga
        public double Co1 { get; set; } // D <= 10
        public double Co2 { get; set; } // D > 10

        // Cửa thu nước (Loại 3)
        public double CuaThu1 { get; set; } // D <= 10
        public double CuaThu2 { get; set; } // D > 10

        // Nắp ga
        public double Nap1 { get; set; } // D <= 10
        public double Nap2 { get; set; } // D > 10
        public double ThepHinh { get; set; } // Thép hình góc kg

        // Tổng cộng
        public double Tong => Day1 + Day2 + Tuong1 + Tuong2 + Tran1 + Tran2 + Co1 + Co2 +
                              CuaThu1 + CuaThu2 + Nap1 + Nap2 + ThepHinh;
    }

    /// <summary>
    /// Kết quả thống kê cốt thép hoàn chỉnh của hố ga
    /// </summary>
    public class SteelScheduleModel
    {
        public string ManholeName { get; init; } = "";
        public List<BarScheduleItem> Items { get; init; } = new();
        public SteelSummary Summary { get; init; } = new();

        /// <summary>
        /// Phân nhóm theo cấu kiện cho Tờ 5 (Bar Bending Schedule)
        /// </summary>
        public Dictionary<string, List<BarScheduleItem>> GroupedItems { get; init; } = new();
    }

    /// <summary>
    /// Động cơ chiết tính khối lượng và lập bảng thống kê cốt thép (DBTool.Core.ScheduleEngine)
    /// </summary>
    public class ScheduleEngine
    {
        public static SteelScheduleModel CalculateSchedule(
            NormalizedManholeSpec spec,
            List<BarInstance> barInstances,
            Dictionary<string, SteelAngleRecord> angleCatalog)
        {
            var items = new List<BarScheduleItem>();
            var summary = new SteelSummary();

            foreach (var b in barInstances)
            {
                double totalLenM = (b.LengthMm * b.Quantity) / 1000.0;
                double q = 0;

                if (b.IsAngleSteel)
                {
                    if (angleCatalog.TryGetValue(b.AngleCode, out var angleRec))
                    {
                        q = angleRec.WeightKgPerMeter;
                    }
                    else
                    {
                        // Fallback ước lượng theo bảng thép góc
                        q = 1.36; // V30x30x3
                    }
                }
                else
                {
                    // Công thức khối lượng riêng tiêu chuẩn: q = pi * (d/1000)^2 * 7850 / 4
                    q = (Math.PI * Math.Pow(b.Diameter / 1000.0, 2) * 7850.0) / 4.0;
                }

                double totalWeight = totalLenM * q;

                var item = new BarScheduleItem
                {
                    ComponentGroup = b.ComponentGroup,
                    BarMark = b.BarMark,
                    Diameter = b.Diameter,
                    AngleCode = b.AngleCode,
                    Shape = b.Shape,
                    Quantity = b.Quantity,
                    LengthMm = b.LengthMm,
                    TotalLengthM = totalLenM,
                    UnitWeightKgM = q,
                    TotalWeightKg = totalWeight,
                    L = b.L,
                    LT = b.LT,
                    LP = b.LP,
                    L1T = b.L1T,
                    L2T = b.L2T,
                    L3 = b.L3,
                    L1P = b.L1P,
                    L2P = b.L2P,
                    Note = b.Note
                };
                items.Add(item);

                // Phân nhóm tổng hợp khối lượng
                AccumulateSummary(summary, item);
            }

            // Gom nhóm cho Tờ 5
            var grouped = new Dictionary<string, List<BarScheduleItem>>();
            foreach (var item in items)
            {
                if (!grouped.ContainsKey(item.ComponentGroup))
                {
                    grouped[item.ComponentGroup] = new List<BarScheduleItem>();
                }
                grouped[item.ComponentGroup].Add(item);
            }

            return new SteelScheduleModel
            {
                ManholeName = spec.Name,
                Items = items,
                Summary = summary,
                GroupedItems = grouped
            };
        }

        private static void AccumulateSummary(SteelSummary summary, BarScheduleItem item)
        {
            string group = item.ComponentGroup.ToLowerInvariant();
            bool isSmallDia = item.Diameter <= 10;
            double w = item.TotalWeightKg;

            if (item.IsAngleSteel)
            {
                summary.ThepHinh += w;
                return;
            }

            if (group.Contains("đáy") || group.Contains("day"))
            {
                if (isSmallDia) summary.Day1 += w;
                else summary.Day2 += w;
            }
            else if (group.Contains("tường") || group.Contains("tuong") || group.Contains("thang") || group.Contains("đai"))
            {
                if (isSmallDia) summary.Tuong1 += w;
                else summary.Tuong2 += w;
            }
            else if (group.Contains("trần") || group.Contains("tran") || group.Contains("gia cường"))
            {
                if (isSmallDia) summary.Tran1 += w;
                else summary.Tran2 += w;
            }
            else if (group.Contains("cổ") || group.Contains("co"))
            {
                if (isSmallDia) summary.Co1 += w;
                else summary.Co2 += w;
            }
            else if (group.Contains("thu") || group.Contains("cửa thu"))
            {
                if (isSmallDia) summary.CuaThu1 += w;
                else summary.CuaThu2 += w;
            }
            else if (group.Contains("nắp") || group.Contains("nap"))
            {
                if (isSmallDia) summary.Nap1 += w;
                else summary.Nap2 += w;
            }
            else
            {
                // Mặc định cho vào Tường
                if (isSmallDia) summary.Tuong1 += w;
                else summary.Tuong2 += w;
            }
        }
    }
}
