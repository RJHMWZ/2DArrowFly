// Copy into an isolated Unity project's Assets/Editor to run
// HuePerformanceGeometryChecks.Run. This file is not part of the playable.
using System;
using System.Collections.Generic;
using System.IO;
using HueRelay;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class HuePerformanceGeometryChecks
{
    static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    public static void Run()
    {
        try
        {
            var geometry = new HueGeometry();
            using (var mesh = new VertexHelper())
            {
                geometry.origin = new Vector2(20, 30);
                geometry.zoom = 2;
                geometry.Disc(new Vector2(10, 15), 5, Color.blue);
                geometry.Populate(mesh);
                Require(mesh.currentVertCount == 4 && mesh.currentIndexCount == 6, "Circle must use one quad.");
                var v = UIVertex.simpleVert;
                mesh.PopulateUIVertex(ref v, 0);
                Require(v.normal == Vector3.forward && Mathf.Abs(v.uv1.x - 5) < .0001f
                    && Mathf.Abs(v.uv1.y - .325f) < .0001f, "Circle shader metadata changed.");
                Require(Vector3.Distance(v.position, new Vector3(29.35f, -49.35f)) < .001f, "Circle transform changed.");
                Require(v.color.Equals((Color32)Color.blue), "Circle color changed.");

                geometry.Clear();
                geometry.Arc(Vector2.zero, 100, 0, Mathf.PI * 2, 3, Color.white);
                geometry.Populate(mesh);
                int segments = mesh.currentVertCount / 4 - 1;
                Require(segments > 0 && segments < 80, "Ring tessellation is excessive.");
                float error = 102.15f * (1 - Mathf.Cos(Mathf.PI / segments));
                Require(error <= .201f, "Ring chord error exceeds the visual tolerance.");
                var stream = new List<UIVertex>();
                mesh.GetUIVertexStream(stream);
                Require(stream.Count == mesh.currentIndexCount, "Invalid ring indices.");

                geometry.Clear();
                geometry.Shadow(new Rect(0, 0, 100, 60), 12, 20, 4, .1f);
                geometry.Populate(mesh);
                mesh.PopulateUIVertex(ref v, 0);
                Require(v.normal == new Vector3(12, 10, 0) && v.uv1.x == 50 && v.uv1.y == 30,
                    "Shadow data conflicts with circle metadata.");
                geometry.Clear();
                geometry.Populate(mesh);
                Require(mesh.currentVertCount == 0 && mesh.currentIndexCount == 0, "Expired geometry retained.");
            }

            var go = new GameObject("Pool check", typeof(RectTransform), typeof(CanvasRenderer), typeof(HueSurface));
            try
            {
                var surface = go.GetComponent<HueSurface>();
                surface.Reserve(2);
                surface.Begin();
                surface.Label("+10", Vector2.zero, 20, Color.white);
                surface.End();
                Require(go.GetComponentsInChildren<Text>().Length == 1, "Text pool did not activate exactly one slot.");
                surface.Begin(); surface.End();
                Require(go.GetComponentsInChildren<Text>().Length == 0, "Expired pooled text retained.");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
            Require(!ShaderUtil.ShaderHasError(Shader.Find("HueRelay/Canvas")), "Canvas shader has compilation errors.");
            Directory.CreateDirectory("Logs/Performance");
            File.WriteAllText("Logs/Performance/geometry.txt", "PASS: circle quad/metadata/transform/color, arc error/index budget, shadow channels, empty mesh, pooled text expiry, shader compilation\n");
            HueRelay.Editor.HuePlayableTools.BatchValidate();
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
