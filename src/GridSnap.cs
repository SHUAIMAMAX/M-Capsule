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
        static int Index(double value,double origin,double step,int count)
        {
            if(Double.IsNaN(value)||Double.IsInfinity(value))return 0;
            return Math.Max(0,Math.Min(count-1,(int)Math.Round((value-origin)/step,MidpointRounding.AwayFromZero)));
        }
    }
}
