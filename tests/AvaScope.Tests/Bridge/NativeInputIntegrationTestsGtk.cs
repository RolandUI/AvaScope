using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using AvaScope.Bridge;

namespace AvaScope.Tests.Bridge;

[Collection(BridgeCollectionDefinition.Name)]
public sealed class NativeInputIntegrationTestsGtk
{
    [NativeInputFact]
    public async Task GtkPickerDispatchRunsWhileDefaultPrioritySourcesRemainReady()
    {
        if (!OperatingSystem.IsLinux()) return;
        // Exercise the actual bridge queue on this test process's GLib context. A
        // continuously ready normal-priority source reproduces idle starvation.
        var runtime = typeof(AvaScopeBridgeRuntime);
        var picker = runtime.GetNestedType("GtkPicker", BindingFlags.NonPublic)!;
        var state = runtime.GetNestedType("NativeDialogState", BindingFlags.NonPublic)!;
        var createState = Expression.Lambda(typeof(Func<>).MakeGenericType(state),
            Expression.New(state.GetConstructor([typeof(bool), typeof(string)])!, Expression.Constant(false), Expression.Constant(null, typeof(string)))).Compile();
        var dispatch = picker.GetMethod("InvokeAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
        SourceCallback busyCallback = _ => 1;
        var source = g_idle_source_new();
        var ticks = 0;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        Task? work = null;
        var completedUnderLoad = false;
        try
        {
            g_source_set_priority(source, 0);
            g_source_set_callback(source, busyCallback, 0, 0);
            g_source_attach(source, 0);
            work = (Task)dispatch.Invoke(null, [createState, timeout.Token])!;
            var timer = Stopwatch.StartNew();
            while (!work.IsCompleted && timer.Elapsed < TimeSpan.FromSeconds(2))
            {
                g_main_context_iteration(0, 0); ticks++;
                await Task.Delay(1);
            }
            completedUnderLoad = work.IsCompletedSuccessfully;
        }
        finally
        {
            g_source_destroy(source); g_source_unref(source);
            await timeout.CancelAsync();
            if (work is not null) try { await work; } catch (OperationCanceledException) { }
            GC.KeepAlive(busyCallback);
        }
        Assert.True(ticks > 0);
        Assert.True(completedUnderLoad, "The bridge's GTK queue was starved by a ready default-priority source.");
    }

    private const string Glib = "libglib-2.0.so.0";
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int SourceCallback(nint data);
    [DllImport(Glib)] private static extern nint g_idle_source_new();
    [DllImport(Glib)] private static extern void g_source_set_priority(nint source, int priority);
    [DllImport(Glib)] private static extern void g_source_set_callback(nint source, SourceCallback callback, nint data, nint destroy);
    [DllImport(Glib)] private static extern uint g_source_attach(nint source, nint context);
    [DllImport(Glib)] private static extern int g_main_context_iteration(nint context, int mayBlock);
    [DllImport(Glib)] private static extern void g_source_destroy(nint source);
    [DllImport(Glib)] private static extern void g_source_unref(nint source);
}
