using System;
using System.Runtime.InteropServices;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace Linkpearl;

public class TestingArea {

  private int clockTick = 0;
  private int updateTick = 0;
  private TimeSpan lastRun = TimeSpan.Zero;
  private TimeSpan rate;

  public TestingArea(int rateMS = 20) {
    rate = new TimeSpan(0, 0, 0, 0, rateMS);
  }

  public void FrameworkUpdate(IFramework framework) {
    // execution rate control
    lastRun = lastRun.Add(framework.UpdateDelta);
    var doRun = lastRun.CompareTo(rate) > -1;
    D.Log.Debug($"{updateTick}/{++clockTick} - {framework.UpdateDelta.ToString()} - {this.lastRun} - {doRun}");
    if (!doRun) return;
    lastRun = TimeSpan.Zero;

    // actual work below
    D.Log.Debug($"{++updateTick} : work work");

  }
}