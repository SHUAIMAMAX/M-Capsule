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
        public double StepX, StepY, OriginX, OriginY, IconSize;
        public GridPoint[] Icons=new GridPoint[0];
    }
    public sealed class CapsuleDimensions
    {
        public double Icon, Width, Height, IconRow, LabelRow;
    }
    public static class CapsuleSizing
    {
        public static CapsuleDimensions Calculate(DesktopGrid desktop)
        {
            double icon=desktop==null||desktop.IconSize<16||desktop.IconSize>256?64:desktop.IconSize;
            double stepX=desktop==null||desktop.StepX<40?96:desktop.StepX;
            double stepY=desktop==null||desktop.StepY<40?112:desktop.StepY;
            icon=Math.Min(icon,Math.Min(stepX-8,stepY-26));
            double width=Math.Min(stepX-2,Math.Max(48,icon+18));
            double height=Math.Min(stepY-2,icon+30);
            return new CapsuleDimensions{Icon=icon,Width=width,Height=height,IconRow=height-26,LabelRow=26};
        }
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
            // Align capsule window origins with Explorer item-cell origins, including row zero.
            int minCol=(int)Math.Ceiling((left-desktop.OriginX)/desktop.StepX);
            int maxCol=(int)Math.Floor((right-width-desktop.OriginX)/desktop.StepX);
            int minRow=(int)Math.Ceiling((top-desktop.OriginY)/desktop.StepY);
            int maxRow=(int)Math.Floor((bottom-height-desktop.OriginY)/desktop.StepY);
            if(minCol>maxCol||minRow>maxRow)return Place(x,y,left,top,right,bottom,width,height,capsules);
            var other=capsules==null?new GridPoint[0]:capsules.Where(p=>p!=null).ToArray();
            GridPoint best=null;double bestScore=Double.PositiveInfinity;
            for(int row=minRow;row<=maxRow;row++)for(int col=minCol;col<=maxCol;col++)
            {
                double px=desktop.OriginX+col*desktop.StepX,py=desktop.OriginY+row*desktop.StepY;
                if(other.Any(p=>Overlaps(px,py,width,height,p.X,p.Y,width,height)))continue;
                double score=Math.Pow((px-x)/desktop.StepX,2)+Math.Pow((py-y)/desktop.StepY,2);
                if(score<bestScore){bestScore=score;best=new GridPoint(px,py);}
            }
            return best??Place(x,y,left,top,right,bottom,width,height,capsules);
        }
        public static bool CoversDesktopIcon(double x,double y,double width,double height,DesktopGrid desktop)
        {
            if(desktop==null||desktop.Icons==null)return false;
            return desktop.Icons.Any(p=>p!=null&&Overlaps(x,y,width,height,p.X,p.Y,desktop.StepX,desktop.StepY));
        }
        public static GridPoint SuggestSpawn(double left,double top,double right,double bottom,double width,double height,
            IEnumerable<GridPoint> capsules,DesktopGrid desktop)
        {
            double centerX=left+(right-left-width)/2,centerY=top+(bottom-top-height)/2;
            if(desktop!=null&&desktop.Icons!=null&&desktop.StepX>=40&&desktop.StepY>=40)
            {
                var icons=desktop.Icons.Where(p=>p!=null&&p.X>=left&&p.X<right&&p.Y>=top&&p.Y<bottom).ToArray();
                double capacity=Math.Max(1,Math.Ceiling((right-left)/desktop.StepX)*Math.Ceiling((bottom-top)/desktop.StepY));
                if(icons.Length>capacity/3)
                {
                    int leftCount=icons.Count(p=>p.X<left+(right-left)/2);
                    int topCount=icons.Count(p=>p.Y<top+(bottom-top)/2);
                    if(leftCount>=Math.Ceiling(icons.Length*0.75))centerX=left+(right-left)*0.75-width/2;
                    else if(topCount>=Math.Ceiling(icons.Length*0.9))centerY=top+(bottom-top)*0.75-height/2;
                }
            }
            return PlaceOnDesktop(centerX,centerY,left,top,right,bottom,width,height,capsules,desktop);
        }
        public static GridPoint[] RightCluster(double left,double top,double width,double height,double capsuleWidth,double capsuleHeight)
        {
            return Enumerable.Range(0,5).Select(i=>new GridPoint(
                left+width*(i%2==0?0.70:0.80)-capsuleWidth/2,
                top+height*(0.35+0.15*(i/2))-capsuleHeight/2)).ToArray();
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
