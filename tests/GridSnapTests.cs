using System;
using System.Collections.Generic;
using System.Linq;
using PocketGrid;

public static class GridSnapTests
{
    static int checks;
    static void Check(bool condition,string label)
    {
        if(!condition)throw new Exception(label);
        checks++;
    }
    public static void Main()
    {
        var none=new GridPoint[0];
        var first=GridSnap.Place(31,30,0,0,1920,1080,88,98,none);
        Check(first.X==24&&first.Y==24,"desktop origin snaps to first grid cell");
        var moved=GridSnap.Place(130,180,0,0,1920,1080,88,98,none);
        Check(moved.X==120&&moved.Y==136,"drag snaps to nearest grid cell");
        var reserved=GridSnap.Place(130,180,0,0,1920,1080,88,98,new[]{moved});
        Check(reserved.X!=moved.X||reserved.Y!=moved.Y,"occupied cell is avoided");
        Check((reserved.X-24)%GridSnap.StepX==0&&(reserved.Y-24)%GridSnap.StepY==0,"fallback stays on the grid");
        var secondMonitor=GridSnap.Place(-1830,145,-1920,0,0,1080,88,98,none);
        Check(secondMonitor.X==-1800&&secondMonitor.Y==136,"negative-coordinate monitor has its own grid");
        var edge=GridSnap.Place(100000,100000,0,0,800,600,88,98,none);
        Check(edge.X>=0&&edge.Y>=0&&edge.X+88<=800&&edge.Y+98<=600,"screen edge remains visible");
        var invalid=GridSnap.Place(double.NaN,double.PositiveInfinity,0,0,1920,1080,88,98,none);
        Check(invalid.X==24&&invalid.Y==24,"invalid saved positions recover to grid origin");
        var positions=new List<GridPoint>();
        for(int i=0;i<32;i++)positions.Add(GridSnap.Place(24,24,0,0,1920,1080,88,98,positions));
        var seen=new HashSet<string>();
        foreach(var p in positions)Check(seen.Add(p.X+","+p.Y),"32 capsules have unique cells");
        var stable=GridSnap.Place(moved.X,moved.Y,0,0,1920,1080,88,98,none);
        Check(stable.X==moved.X&&stable.Y==moved.Y,"reposition is stable");
        var desktop=new DesktopGrid{StepX=120,StepY=130,OriginX=20,OriginY=15,
            Icons=new[]{new GridPoint(20,15),new GridPoint(140,15),new GridPoint(260,15)}};
        var aligned=GridSnap.PlaceOnDesktop(270,20,0,0,1920,1080,88,98,none,desktop);
        Check(aligned.X!=276||aligned.Y!=31,"desktop icon cell is never reused");
        Check(desktop.Icons.All(p=>aligned.X>=p.X+120||aligned.X+88<=p.X||aligned.Y>=p.Y+130||aligned.Y+98<=p.Y),"capsule rectangle does not cover any Explorer icon cell");
        Check((aligned.X-36)%120==0&&(aligned.Y-31)%130==0,"desktop grid phase and centered anchor match Explorer spacing");
        var second=GridSnap.PlaceOnDesktop(270,20,0,0,1920,1080,88,98,new[]{aligned},desktop);
        Check(second.X!=aligned.X||second.Y!=aligned.Y,"capsule avoids an occupied native grid cell");
        var multi=new DesktopGrid{StepX=120,StepY=130,OriginX=-1910,OriginY=15,Icons=new[]{new GridPoint(-1910,15)}};
        var nativeSecond=GridSnap.PlaceOnDesktop(-1780,10,-1920,0,0,1080,88,98,none,multi);
        Check(nativeSecond.X>=-1920&&nativeSecond.X+88<=0,"native grid respects negative-origin monitor");
        Check(GridSnap.PlaceOnDesktop(31,30,0,0,1920,1080,88,98,none,null).X==24,"missing Shell view falls back to old grid");
        Console.WriteLine("PASS: "+checks+" grid placement checks.");
    }
}
