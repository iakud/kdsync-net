namespace Kdsync;

internal delegate TValue ValueReader<out TValue>(ref ParseContext ctx);