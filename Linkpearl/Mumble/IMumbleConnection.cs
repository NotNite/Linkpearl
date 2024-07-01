using System;

namespace Linkpearl;

public interface IMumbleConnection : IDisposable {
    public void Update(MumbleAvatar avatar);
}
