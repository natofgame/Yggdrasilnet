using System.Diagnostics;

namespace Yggdrasilnet.Server.Services;

public sealed class NetIoMetricsTracker {
    private long _sendCalls;
    private long _sendBytes;
    private long _sendSerializeTicks;
    private long _sendSocketTicks;
    private long _receiveCalls;
    private long _receiveBytes;
    private long _receiveDecodeTicks;

    public void RecordSend(int bytes, long serializeTicks, long socketTicks) {
        _sendCalls++;
        _sendBytes += bytes;
        _sendSerializeTicks += serializeTicks;
        _sendSocketTicks += socketTicks;
    }

    public void RecordReceive(int bytes, long decodeTicks) {
        _receiveCalls++;
        _receiveBytes += bytes;
        _receiveDecodeTicks += decodeTicks;
    }

    public NetIoMetricsSnapshot CollectAndReset() {
        var sendCalls = _sendCalls;
        var sendBytes = _sendBytes;
        var sendSerializeTicks = _sendSerializeTicks;
        var sendSocketTicks = _sendSocketTicks;
        var receiveCalls = _receiveCalls;
        var receiveBytes = _receiveBytes;
        var receiveDecodeTicks = _receiveDecodeTicks;

        _sendCalls = 0;
        _sendBytes = 0;
        _sendSerializeTicks = 0;
        _sendSocketTicks = 0;
        _receiveCalls = 0;
        _receiveBytes = 0;
        _receiveDecodeTicks = 0;

        var ticksToMilliseconds = 1000d / Stopwatch.Frequency;
        return new NetIoMetricsSnapshot(
            sendCalls,
            sendBytes,
            sendSerializeTicks * ticksToMilliseconds,
            sendSocketTicks * ticksToMilliseconds,
            receiveCalls,
            receiveBytes,
            receiveDecodeTicks * ticksToMilliseconds
        );
    }
}

public readonly record struct NetIoMetricsSnapshot(
    long SendCalls,
    long SendBytes,
    double SendSerializeMs,
    double SendSocketMs,
    long ReceiveCalls,
    long ReceiveBytes,
    double ReceiveDecodeMs
);
