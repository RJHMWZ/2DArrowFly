// Standalone geometry check: compile with HueHudGeometry.cs and UnityEngine.CoreModule.dll.
using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using HueRelay;

public static class CheckHudGeometry
{
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    public static void Main()
    {
        int count = 0;
        var samples = new StringBuilder();
        foreach (var size in new[] { new Vector2(390,667), new Vector2(390,844), new Vector2(1080,1920),
            new Vector2(1080,2400), new Vector2(1080,2520), new Vector2(1170,2532), new Vector2(844,390) })
        foreach (float safeTop in new[] { 0f, 44f, 76f })
        foreach (var board in new[] { new Vector2(4,6), new Vector2(5,6), new Vector2(9,12), new Vector2(12,16), new Vector2(5,18) })
        {
            float physical = size.x > size.y ? size.y / 800 : size.x / 390;
            float width = size.x / physical, height = size.y / physical;
            float scale = Mathf.Clamp(Mathf.Min(width/390,height/800),.75f,3.5f);
            float appWidth = Mathf.Min(width,460*scale), headerWidth = appWidth/scale-28;
            float stageY = safeTop+(10+70+10+56+10+6+8)*scale;
            float stageHeight = height-20-(76+12+16)*scale-stageY;
            float cell = Mathf.Max(1, Mathf.Min(72*scale, Mathf.Floor((appWidth-44*scale)/board.x),
                Mathf.Floor((stageHeight-16*scale)/board.y)));
            float boardTop = stageY+(stageHeight-board.y*cell)*.5f;
            var hud = HueHudGeometry.Calculate(headerWidth,board.x*cell/scale,
                (boardTop-safeTop-10*scale)/scale,80,56,10,4);
            Require(hud.head.yMin == 80, "Stats must sit directly below toolbar metadata.");
            Require(hud.head.xMin >= 0 && hud.head.xMax <= headerWidth, "HUD escaped width.");
            Require(hud.score.width == hud.moves.width && !hud.score.Overlaps(hud.moves), "Primary cards overlap.");
            Require(Mathf.Abs(hud.head.y + hud.level.center.y - 22) < .01f,
                "Level must align with the center of the back button.");
            Require(hud.level.yMax < hud.best.yMin && hud.best.yMax < hud.score.yMin,
                "Best must sit below level and above primary values.");
            Require(Mathf.Abs(hud.score.yMin-hud.best.yMax-10)<.01f,
                "Best/stats gap must remain compact.");
            float oldStageY = safeTop+(10+64+10+108+10+16)*scale;
            float oldStageHeight = height-20-(76+12+16)*scale-oldStageY;
            float oldCell = Mathf.Max(1,Mathf.Min(64*scale,Mathf.Floor((appWidth-60*scale)/board.x),
                Mathf.Floor((oldStageHeight-48*scale)/board.y)));
            Require(cell>=oldCell,"Compact HUD reduced the board size.");
            Require(Mathf.Abs(hud.head.x+hud.level.center.x-headerWidth*.5f)<.01f,
                "Toolbar metadata must be centered.");
            Require(hud.head.x+hud.level.xMax <= headerWidth-112+.01f,
                "Metadata entered the right host controls.");
            float progressBottom = safeTop+10*scale+hud.progress.yMax*scale;
            Require(progressBottom+17*scale <= boardTop+.01f, "HUD or progress overlaps board frame.");
            Require(boardTop+board.y*cell <= height-20-(76+12+16)*scale, "Board overlaps tool bar.");
            // Preserve one portrait geometry sample for a visual layout review.
            if (size.x==390 && size.y==844 && safeTop==44 && board.x==5 && board.y==6)
            {
                foreach(var pair in new[] { Tuple.Create("head",hud.head),Tuple.Create("level",hud.level),
                    Tuple.Create("score",hud.score),Tuple.Create("moves",hud.moves),Tuple.Create("best",hud.best),
                    Tuple.Create("progress",hud.progress),Tuple.Create("board",new Rect((headerWidth-board.x*cell/scale)*.5f,
                        (boardTop-safeTop-10*scale)/scale,board.x*cell/scale,board.y*cell/scale)) })
                    samples.AppendLine(string.Format(CultureInfo.InvariantCulture,"{0},{1},{2},{3},{4}",pair.Item1,
                        pair.Item2.x,pair.Item2.y,pair.Item2.width,pair.Item2.height));
            }
            count++;
        }
        File.WriteAllText("Temp/DebugToolsValidation/hud-geometry.csv",samples.ToString());
        Console.WriteLine("PASS: "+count+" viewport / safe-area / board geometry cases.");
    }
}
