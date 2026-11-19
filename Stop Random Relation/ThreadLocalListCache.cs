namespace Stop_Random_Relation;

/// <summary>
/// Thread-safe, re-entrancy-safe thread-local pool for temporary <see cref="List{T}"/> buffers.
/// </summary>
/// <typeparam name="T">The element type stored in the pooled list.</typeparam>
/// <remarks>
/// Renting detaches the cached buffer from the thread-static slot so nested or
/// re-entrant calls never alias the same list, and returning clears all elements
/// immediately so references are not pinned across GC cycles.
/// </remarks>
internal static class ThreadLocalListCache<T>
{
  [ThreadStatic]
  private static List<T>? _cached;

  /// <summary>
  /// Rents an empty <see cref="List{T}"/> buffer for the current thread.
  /// </summary>
  /// <returns>An empty list instance owned by the caller until returned.</returns>
  public static List<T> Rent()
  {
    List<T>? buffer = _cached;
    if (buffer is null)
    {
      return [];
    }

    _cached = null;
    buffer.Clear();
    return buffer;
  }

  /// <summary>
  /// Clears <paramref name="buffer"/> and returns it to the current thread's cache.
  /// </summary>
  /// <param name="buffer">The list previously obtained from <see cref="Rent"/>.</param>
  public static void Return(List<T> buffer)
  {
    buffer.Clear();
    _cached = buffer;
  }
}
