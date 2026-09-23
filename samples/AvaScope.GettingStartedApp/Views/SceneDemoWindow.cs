using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AvaScope.Bridge;
using AvaScope.Protocol;

namespace AvaScope.GettingStartedApp.Views;

/// <summary>Optional in-memory graph for inspecting semantic objects which have no visual children.</summary>
public sealed class SceneDemoWindow : Window
{
    public SceneDemoWindow(AvaScopeBridgeRuntime runtime)
    {
        Title = "AvaScope semantic graph"; Width = 560; Height = 360;
        var graph = new Graph { Name = "SemanticGraph", Height = 220 };
        var zoom = new Button { Content = "Zoom / pan" };
        var remove = new Button { Content = "Remove / recreate connection" };
        zoom.Click += (_, _) => { graph.Zoomed = !graph.Zoomed; graph.Revision++; graph.InvalidateVisual(); };
        remove.Click += (_, _) => { graph.HasConnection = !graph.HasConnection; graph.ConnectionGeneration = Guid.NewGuid().ToString("N"); graph.Revision++; graph.InvalidateVisual(); };
        Content = new StackPanel
        {
            Margin = new Thickness(16), Spacing = 12,
            Children = { new TextBlock { Text = "Use scene to inspect the graph and select connection A → B." }, graph,
                new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 12, Children = { zoom, remove } } }
        };
        var registrations = new List<IDisposable>();
        Opened += (_, _) =>
        {
            registrations.Add(runtime.RegisterTopLevel(this));
            registrations.Add(runtime.RegisterScene(graph, graph.Capture));
            registrations.Add(runtime.RegisterCustomAction(graph, new("scene.select", context =>
            {
                graph.Selected = context.SceneObject!.Id; graph.Revision++; graph.InvalidateVisual();
                return CustomActionOutcome.Succeeded("Selected the declared graph object.");
            }, "Selects the observed in-memory connection.", requiresSceneObject: true)));
        };
        Closed += (_, _) => { foreach (var registration in registrations) registration.Dispose(); registrations.Clear(); };
    }

    private sealed class Graph : Control
    {
        public bool Zoomed { get; set; }
        public bool HasConnection { get; set; } = true;
        public string ConnectionGeneration { get; set; } = Guid.NewGuid().ToString("N");
        public string? Selected { get; set; }
        public int Revision { get; set; }
        private Matrix Camera => Zoomed ? new(1.25, 0, 0, 1.25, 25, 15) : Matrix.Identity;

        public AvaScopeSceneSnapshot Capture()
        {
            var objects = new List<RuntimeSceneObject>
            {
                new("a", "node-a", "node", "Source A", new(20, 40, 80, 60), Selected == "a"),
                new("b", "node-b", "node", "Destination B", new(280, 40, 80, 60), Selected == "b")
            };
            if (HasConnection) objects.Add(new("a-b", ConnectionGeneration, "connection", "A to B", new(100, 68, 180, 4), Selected == "a-b",
                [new("connects_from", "a", "node-a"), new("connects_to", "b", "node-b")], ["scene.select"]));
            return new(Revision.ToString(System.Globalization.CultureInfo.InvariantCulture), objects, Camera);
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);
            using (context.PushTransform(Camera))
            {
                context.DrawRectangle(Brushes.SteelBlue, null, new Rect(20, 40, 80, 60));
                context.DrawRectangle(Brushes.Teal, null, new Rect(280, 40, 80, 60));
                if (HasConnection) context.DrawLine(new Pen(Selected == "a-b" ? Brushes.Orange : Brushes.Gray, Selected == "a-b" ? 6 : 3), new(100, 70), new(280, 70));
            }
        }
    }
}
