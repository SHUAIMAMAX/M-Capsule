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
        Check(aligned.X==260&&aligned.Y==15,"capsule may use the same first-row cell as a desktop icon");
        Check(GridSnap.CoversDesktopIcon(aligned.X,aligned.Y,88,98,desktop),"overlapping Explorer icon takes visual and click priority");
        Check(!GridSnap.CoversDesktopIcon(500,400,88,98,desktop),"capsule without icon overlap remains visible");
        Check((aligned.X-20)%120==0&&(aligned.Y-15)%130==0,"desktop grid phase starts at Explorer item origin without extra margin");
        var second=GridSnap.PlaceOnDesktop(270,20,0,0,1920,1080,88,98,new[]{aligned},desktop);
        Check(second.X!=aligned.X||second.Y!=aligned.Y,"capsule avoids an occupied native grid cell");
        var multi=new DesktopGrid{StepX=120,StepY=130,OriginX=-1910,OriginY=15,Icons=new[]{new GridPoint(-1910,15)}};
        var nativeSecond=GridSnap.PlaceOnDesktop(-1780,10,-1920,0,0,1080,88,98,none,multi);
        Check(nativeSecond.X>=-1920&&nativeSecond.X+88<=0,"native grid respects negative-origin monitor");
        var compact=new DesktopGrid{StepX=72,StepY=80,OriginX=16,OriginY=12,Icons=new[]{new GridPoint(16,12)}};
        var compactTop=GridSnap.PlaceOnDesktop(18,13,0,0,800,600,88,98,none,compact);
        Check(compactTop.X==16&&compactTop.Y==12,"small desktop icons still allow first-row placement");
        var compactMove=GridSnap.PlaceOnDesktop(180,210,0,0,800,600,88,98,none,compact);
        Check((compactMove.X-16)%72==0&&(compactMove.Y-12)%80==0,"Ctrl-wheel resized grid uses new small spacing");
        var expanded=new DesktopGrid{StepX=150,StepY=170,OriginX=16,OriginY=12,Icons=new[]{new GridPoint(16,12)}};
        var expandedMove=GridSnap.PlaceOnDesktop(180,210,0,0,800,600,88,98,none,expanded);
        Check((expandedMove.X-16)%150==0&&(expandedMove.Y-12)%170==0,"Ctrl-wheel resized grid uses new large spacing");
        Check(GridSnap.PlaceOnDesktop(31,30,0,0,1920,1080,88,98,none,null).X==24,"missing Shell view falls back to old grid");
        Console.WriteLine("PASS: "+checks+" grid placement checks.");
    }
}
