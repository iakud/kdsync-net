namespace Kdsync;

internal delegate void ValueWriter<T>(ref WriteContext ctx, T value);