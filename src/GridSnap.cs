using System;
using System.Collections.Generic;
using System.Linq;

namespace PocketGrid
{
    // Coordinates are WPF device-independent pixels, with one grid per monitor work area.
    public sealed class GridPoint
    {
        public double X, Y;
        public GridPoint(double x, double y) { X=x; Y=y; }
    }
    public sealed class DesktopGrid
    {
        public double StepX, StepY, OriginX, OriginY;
        public GridPoint[] Icons=new GridPoint[0];
    }
    public static class GridSnap
    {
        public const double StepX=96, StepY=112, Margin=24;
        public static GridPoint Place(double x,double y,double left,double top,double right,double bottom,
            double width,double height,IEnumerable<GridPoint> occupied)
        {
            double originX=left+Margin, originY=top+Margin;
            int columns=Math.Max(1,(int)Math.Floor((right-8-width-originX)/StepX)+1);
            int rows=Math.Max(1,(int)Math.Floor((bottom-8-height-originY)/StepY)+1);
            int wantX=Index(x,originX,StepX,columns), wantY=Index(y,originY,StepY,rows);
            var taken=new HashSet<int>();
            foreach(var point in occupied??Enumerable.Empty<GridPoint>())
            {
                if(point==null||point.X<left||point.X>=right||point.Y<top||point.Y>=bottom)continue;
                int col=Index(point.X,originX,StepX,columns),row=Index(point.Y,originY,StepY,rows);
                taken.Add(row*columns+col);
            }
            int bestX=wantX,bestY=wantY;double best=Double.PositiveInfinity;
            for(int row=0;row<rows;row++)for(int col=0;col<columns;col++)
            {
                if(taken.Contains(row*columns+col))continue;
                double score=Math.Pow(col-wantX,2)+Math.Pow(row-wantY,2);
                if(score<best){best=score;bestX=col;bestY=row;}
            }
            return new GridPoint(originX+bestX*StepX,originY+bestY*StepY);
        }
        public static GridPoint PlaceOnDesktop(double x,double y,double left,double top,double right,double bottom,
            double width,double height,IEnumerable<GridPoint> capsules,DesktopGrid desktop)
        {
            if(desktop==null||desktop.StepX<40||desktop.StepY<40||desktop.StepX>500||desktop.StepY>500)
                return Place(x,y,left,top,right,bottom,width,height,capsules);
            // Explorer returns item-cell origins, not the center of the visible artwork.
            double offsetX=(desktop.StepX-width)/2,offsetY=(desktop.StepY-height)/2;
            int minCol=(int)Math.Ceiling((left+8-desktop.OriginX-offsetX)/desktop.StepX);
            int maxCol=(int)Math.Floor((right-8-width-desktop.OriginX-offsetX)/desktop.StepX);
            int minRow=(int)Math.Ceiling((top+8-desktop.OriginY-offsetY)/desktop.StepY);
            int maxRow=(int)Math.Floor((bottom-8-height-desktop.OriginY-offsetY)/desktop.StepY);
            if(minCol>maxCol||minRow>maxRow)return Place(x,y,left,top,right,bottom,width,height,capsules);
            var other=capsules==null?new GridPoint[0]:capsules.Where(p=>p!=null).ToArray();
            var icons=desktop.Icons??new GridPoint[0];
            GridPoint best=null;double bestScore=Double.PositiveInfinity;
            for(int row=minRow;row<=maxRow;row++)for(int col=minCol;col<=maxCol;col++)
            {
                double px=desktop.OriginX+col*desktop.StepX+offsetX,py=desktop.OriginY+row*desktop.StepY+offsetY;
                if(other.Any(p=>Overlaps(px,py,width,height,p.X,p.Y,width,height)))continue;
                if(icons.Any(p=>Overlaps(px,py,width,height,p.X,p.Y,desktop.StepX,desktop.StepY)))continue;
                double score=Math.Pow((px-x)/desktop.StepX,2)+Math.Pow((py-y)/desktop.StepY,2);
                if(score<bestScore){bestScore=score;best=new GridPoint(px,py);}
            }
            return best??Place(x,y,left,top,right,bottom,width,height,capsules);
        }
        static bool Overlaps(double ax,double ay,double aw,double ah,double bx,double by,double bw,double bh)
        {
            return ax<bx+bw&&ax+aw>bx&&ay<by+bh&&ay+ah>by;
        }
        static int Index(double value,double origin,double step,int count)
        {
            if(Double.IsNaN(value)||Double.IsInfinity(value))return 0;
            return Math.Max(0,Math.Min(count-1,(int)Math.Round((value-origin)/step,MidpointRounding.AwayFromZero)));
        }
    }
}
