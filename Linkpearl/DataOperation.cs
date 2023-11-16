using System;
using Dalamud.Plugin.Services;

namespace Linkpearl;

public abstract class DataOperation : IDisposable {

    private uint clockTick = 0;
    protected uint updateTick = 0;
    private TimeSpan lastRun = TimeSpan.Zero;
    private TimeSpan rate;

    public DataOperation(int rateMS) {
        rate = new TimeSpan(0, 0, 0, 0, rateMS);
    }

    protected bool accumulateDeltaTime(TimeSpan delta) {
        lastRun = lastRun.Add(delta);
        clockTick += 1;
        if (lastRun.CompareTo(rate) > -1) {
            updateTick += 1;
            //D.Log.Debug($"{updateTick}/{clockTick} - {delta.ToString()} - {this.lastRun} - work");
            lastRun = TimeSpan.Zero;
            return true;
        }
        //D.Log.Debug($"{updateTick}/{clockTick} - {delta.ToString()} - {this.lastRun} - skip");
        return false;
    }

    public void RateLimitedUpdate(IFramework framework) {
        if (!accumulateDeltaTime(framework.UpdateDelta)) return;
        performUpdate();
    }

    protected abstract void performUpdate();
    public abstract void Dispose();
}
