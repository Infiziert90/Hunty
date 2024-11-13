using System;
using Dalamud.Plugin.Ipc;

namespace Hunty.IPC;

public class TeleportConsumer
{
    private bool Available;
    private long TimeSinceLastCheck;

    public TeleportConsumer() => Subscribe();

    public bool IsAvailable
    {
        get
        {
            if (TimeSinceLastCheck + 5000 > Environment.TickCount64)
            {
                return Available;
            }

            try
            {
                TimeSinceLastCheck = Environment.TickCount64;

                IsInitialized.InvokeFunc();
                Available = true;
            }
            catch
            {
                Available = false;
            }

            return Available;
        }
    }

    private ICallGateSubscriber<bool> IsInitialized = null!;
    private ICallGateSubscriber<uint, byte, bool> Teleport = null!;

    private void Subscribe()
    {
        try
        {
            IsInitialized = Plugin.PluginInterface.GetIpcSubscriber<bool>("Teleport.ChatMessage");
            Teleport = Plugin.PluginInterface.GetIpcSubscriber<uint, byte, bool>("Teleport");
        }
        catch (Exception e)
        {
            Plugin.Log.Debug($"Failed to subscribe to Teleport\nReason: {e}");
        }
    }

    public bool UseTeleport(uint aetheryteId)
    {
        try
        {
            return Teleport.InvokeFunc(aetheryteId, 0);
        }
        catch
        {
            Plugin.ChatGui.PrintError("Teleport plugin is not responding");
            return false;
        }
    }
}