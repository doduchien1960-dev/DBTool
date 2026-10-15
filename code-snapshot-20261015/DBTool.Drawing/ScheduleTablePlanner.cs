using DBTool.Domain.Geometry;
using DBTool.Domain;
using System.Globalization;

namespace DBTool.Drawing;

public sealed record ScheduleTablePage
{
    public ScheduleTablePage(ViewDefinition view,IEnumerable<DrawingPrimitive> primitives)
    { View=view;Primitives=System.Collections.Immutable.ImmutableArray.CreateRange(primitives); }
    public ViewDefinition View { get; }
    public System.Collections.Immutable.ImmutableArray<DrawingPrimitive> Primitives { get; }
}

/// <summary>Model-mm table layout. Metrics are provisional until matched golden typography is certified.</summary>
public static class ScheduleTablePlanner
{
    public static IReadOnlyList<ScheduleTablePage> Plan(IReadOnlyList<TableRowPrimitive> rows, Point2Mm origin,
        double scale, string title, IReadOnlyList<string> headers, string? footer, bool? includeGrid = null)
    {
        if(rows.Count==0) return [];
        if(scale<=0 || !double.IsFinite(scale)) throw new ArgumentOutOfRangeException(nameof(scale));
        var keys=rows[0].Cells.Select(c=>c.ColumnKey).ToArray();
        if(keys.Length==0 || headers.Count!=keys.Length || rows.Any(r=>!r.Cells.Select(c=>c.ColumnKey).SequenceEqual(keys)))
            throw new ArgumentException("Every row must share the ordered table schema.");
        bool isRoundSteelTable = rows.Any(r => r.MaterialKind == DrawingMaterialKind.RoundSteel && r.ViewId != "QuantityTable" && r.Role != "Quantity/Row");
        bool emitGrid = includeGrid ?? !isRoundSteelTable;
        double h=2*scale, pitch=4*scale, limit=240*scale, pad=scale;
        string Display(TableCell c) => c.Kind == TableCellKind.Text
            ? c.Text!
            : (c.Number!.Value % 1 == 0
                ? c.Number.Value.ToString("0", CultureInfo.InvariantCulture)
                : c.Number.Value.ToString("0.00", CultureInfo.InvariantCulture));
        // Conservative character envelope includes padding; native font extents remain adapter-verified.
        var widths=keys.Select((_,i)=>Math.Max(10*scale,Math.Max(headers[i].Length,rows.Max(r=>Display(r.Cells[i]).Length))*h+2*pad)).ToArray();
        double width=widths.Sum();
        if(width>390*scale)throw new ArgumentException("Table schema exceeds the printable A3 width.");
        if(title.Length*h>width || (footer?.Length??0)*h>width) throw new ArgumentException("Table title/footer exceeds its text envelope.");
        var pages=new List<ScheduleTablePage>();
        int cursor=0;
        while(cursor<rows.Count)
        {
            int page=pages.Count;
            string id=page==0?rows[0].ViewId:$"{rows[0].ViewId}/Page/{page+1}";
            var primitives=new List<DrawingPrimitive>();
            double y=limit-pitch;
            void Text(string suffix,string role,string content,double x,double atY)=>primitives.Add(new TextPrimitive(
                $"{id}/{suffix}",role,id,null,null,null,new(x,atY),content,h,0));
            void Line(string suffix,Point2Mm a,Point2Mm b)
            {
                if (!emitGrid) return;
                primitives.Add(new PolylinePrimitive($"{id}/Grid/{suffix}","Schedule/Grid",id,null,null,null,[a,b],false));
            }
            Text("Title","ViewTitle",title,pad,y+pad);
            y-=pitch;
            double x=0;
            for(int c=0;c<keys.Length;c++){Text($"Header/{c}","Schedule/Header",headers[c],x+pad,y+pad);x+=widths[c];}
            Line("Top",new(0,y+pitch),new(width,y+pitch));
            Line("Header",new(0,y),new(width,y));
            string? group=null;
            bool first=true;
            while(cursor<rows.Count)
            {
                var row=rows[cursor];
                string? rawGroup = row.Cells.FirstOrDefault(c => c.ColumnKey == "Component" && c.Kind == TableCellKind.Text)?.Text ?? row.ComponentId;
                string? rowGroup = rawGroup switch
                {
                    "Base" => "BẢN ĐÁY",
                    "Wall" => "THÀNH GA",
                    "Roof" => "BẢN NẮP",
                    "Neck" => "CỔ GA",
                    "CatchBasin" => "CỬA THU",
                    _ => rawGroup
                };
                bool changed=first || group!=rowGroup;
                double needed=pitch*(changed?2:1);
                if(y-needed<5*pitch) break; // reserve native summary envelope plus bottom padding
                if(changed){group=rowGroup;first=false;
                    if((group??"Steel").Length*h>width-2*pad)throw new ArgumentException("Table group exceeds its text envelope.");
                    y-=pitch;Text($"Group/{cursor}","Schedule/Group",group??"Steel",pad,y+pad);Line($"Group/{cursor}",new(0,y),new(width,y));}
                y-=pitch;x=0;
                var placements=new List<TableCellPlacement>();
                for(int c=0;c<keys.Length;c++){
                    double left=row.Cells[c].Kind==TableCellKind.Number?x+widths[c]-pad-Display(row.Cells[c]).Length*h:x+pad;
                    placements.Add(new(keys[c],new(left,y+pad),h));x+=widths[c];
                }
                primitives.Add(new TableRowPrimitive(row.SemanticId,row.Role,id,row.ComponentId,row.OccurrenceId,row.ScheduleMark,row.Mark,row.Cells,row.MaterialKind,placements));
                Line($"Row/{cursor}",new(0,y),new(width,y));
                cursor++;
            }
            if(cursor==rows.Count && footer is not null){y-=10*scale;Text("Footer","Schedule/TotalMass",footer,pad,y+pad);Line("Footer",new(0,y),new(width,y));}
            x=0;
            for(int c=0;c<=keys.Length;c++){Line($"Column/{c}",new(x,y),new(x,limit-pitch));if(c<keys.Length)x+=widths[c];}
            pages.Add(new(new ViewDefinition(id,ViewPurpose.SteelTable,new(origin.X+page*(width+10*scale),origin.Y),new Bounds2Mm(0,0,width,limit),scale),primitives));
        }
        return pages;
    }
}
