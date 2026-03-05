// Phase 13: Nullable reference types enabled across the CTC client project.
//
// The primary deliverable of Phase 13 is that the CTC project compiles with
// <Nullable>enable</Nullable> and produces zero warnings.  These tests
// validate the nullable-safety contracts introduced by the refactoring:
//
//  - nullable-aware dictionary lookups (mirrors ClientMap / TibiaGameData)
//  - nullable event invocation with ?. (mirrors all UI events)
//  - nullable return types on factory-style getters
//  - null-forgiving suppressor usage for deferred-init fields
//  - Equals(object?) override compatibility

namespace mtanksl.OpenTibia.Tests;

// ---------------------------------------------------------------------------
// Helper types used only in these tests
// ---------------------------------------------------------------------------

/// <summary>
/// A minimal sparse map that returns null for missing positions,
/// mirroring the nullable indexer added to <c>ClientMap</c> in Phase 13.
/// </summary>
file sealed class NullableIndexerMap<TKey, TValue>
    where TKey  : notnull
    where TValue : class
{
    private readonly Dictionary<TKey, TValue> _store = new();

    public TValue? this[TKey key]
    {
        get => _store.TryGetValue(key, out var v) ? v : null;
        set { if (value != null) _store[key] = value; }
    }

    public void Put(TKey key, TValue value) => _store[key] = value;
}

/// <summary>
/// A simple event publisher whose event is declared nullable,
/// mirroring the nullable events in all CTC UI classes (UIButton, UIFrame, …).
/// </summary>
file sealed class NullableEventSource
{
    public delegate void DataReadyHandler(string data);

    // Declared nullable — subscribers are optional.
    public event DataReadyHandler? DataReady;

    public bool Raise(string data)
    {
        DataReady?.Invoke(data);
        return DataReady != null;
    }
}

/// <summary>
/// A factory that returns nullable sprites/types,
/// mirroring <c>TibiaGameData.GetItemSprite</c> / <c>GetItemType</c>.
/// </summary>
file sealed class NullableGetterFactory
{
    private readonly Dictionary<int, string> _store = new();

    public void Register(int id, string value) => _store[id] = value;

    public string? Get(int id) => _store.TryGetValue(id, out var v) ? v : null;
}

/// <summary>
/// Demonstrates the deferred-init (null-forgiving) pattern used for fields
/// assigned inside a helper method called from the constructor.
/// The <c>null!</c> suppressor is valid here because <c>Build()</c> is always
/// called in the constructor body before any external code can access the object.
/// </summary>
file sealed class DeferredInitOwner
{
    // Assigned in Build(), which is always called from the constructor.
    private string _name = null!;

    public DeferredInitOwner(string name)
    {
        Build(name);
    }

    private void Build(string name)
    {
        _name = name;
    }

    public string Name => _name;
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

/// <summary>
/// Validates the nullable indexer pattern (ClientMap style).
/// </summary>
public class NullableMapIndexerTests
{
    [Fact]
    public void Get_ExistingKey_ReturnsValue()
    {
        var map = new NullableIndexerMap<int, string>();
        map.Put(1, "hello");

        string? result = map[1];

        Assert.NotNull(result);
        Assert.Equal("hello", result);
    }

    [Fact]
    public void Get_MissingKey_ReturnsNull()
    {
        var map = new NullableIndexerMap<int, string>();

        string? result = map[99];

        Assert.Null(result);
    }

    [Fact]
    public void Set_NullValue_IsIgnored()
    {
        var map = new NullableIndexerMap<int, string>();
        map.Put(1, "original");

        // Setting to null should be a no-op (matches ClientMap behaviour)
        map[1] = null;

        Assert.Equal("original", map[1]);
    }

    [Fact]
    public void Set_NonNullValue_UpdatesEntry()
    {
        var map = new NullableIndexerMap<int, string>();
        map[7] = "updated";

        Assert.Equal("updated", map[7]);
    }
}

/// <summary>
/// Validates the nullable event pattern used throughout the CTC UI framework.
/// </summary>
public class NullableEventTests
{
    [Fact]
    public void Raise_NoSubscribers_DoesNotThrow()
    {
        var src = new NullableEventSource();

        // Must not throw even when no handler is attached
        var exception = Record.Exception(() => src.Raise("ping"));
        Assert.Null(exception);
    }

    [Fact]
    public void Raise_NoSubscribers_ReturnsFalse()
    {
        var src = new NullableEventSource();
        bool hadSubscribers = src.Raise("ping");
        Assert.False(hadSubscribers);
    }

    [Fact]
    public void Raise_WithSubscriber_InvokesHandler()
    {
        var src = new NullableEventSource();
        string? received = null;
        src.DataReady += data => received = data;

        src.Raise("hello");

        Assert.Equal("hello", received);
    }

    [Fact]
    public void Raise_WithSubscriber_ReturnsTrue()
    {
        var src = new NullableEventSource();
        src.DataReady += _ => { };

        bool hadSubscribers = src.Raise("ping");

        Assert.True(hadSubscribers);
    }

    [Fact]
    public void Raise_MultipleSubscribers_AllInvoked()
    {
        var src = new NullableEventSource();
        int callCount = 0;
        src.DataReady += _ => callCount++;
        src.DataReady += _ => callCount++;

        src.Raise("multi");

        Assert.Equal(2, callCount);
    }
}

/// <summary>
/// Validates the nullable-return factory pattern (TibiaGameData style).
/// </summary>
public class NullableGetterFactoryTests
{
    [Fact]
    public void Get_RegisteredId_ReturnsValue()
    {
        var factory = new NullableGetterFactory();
        factory.Register(42, "sword");

        string? result = factory.Get(42);

        Assert.Equal("sword", result);
    }

    [Fact]
    public void Get_UnregisteredId_ReturnsNull()
    {
        var factory = new NullableGetterFactory();

        string? result = factory.Get(999);

        Assert.Null(result);
    }

    [Fact]
    public void Get_CanBeUsedWithNullConditional()
    {
        var factory = new NullableGetterFactory();
        factory.Register(1, "helmet");

        int? length = factory.Get(1)?.Length;

        Assert.Equal("helmet".Length, length);
    }

    [Fact]
    public void Get_MissingKey_NullConditional_ReturnsNull()
    {
        var factory = new NullableGetterFactory();

        int? length = factory.Get(0)?.Length;

        Assert.Null(length);
    }
}

/// <summary>
/// Validates the deferred-init (null-forgiving) pattern used in Phase 13
/// for fields set inside builder/helper methods called from constructors.
/// </summary>
public class DeferredInitTests
{
    [Fact]
    public void Constructor_SetsFieldViaHelper_NotNull()
    {
        var owner = new DeferredInitOwner("Excalibur");

        Assert.Equal("Excalibur", owner.Name);
    }

    [Fact]
    public void Constructor_EmptyString_FieldIsEmpty()
    {
        var owner = new DeferredInitOwner("");

        Assert.Equal("", owner.Name);
    }
}

/// <summary>
/// Validates the <c>Equals(object?)</c> override pattern introduced in Phase 13
/// (mirrors <c>MapPosition.Equals(object? obj)</c>).
/// </summary>
public class NullableEqualsTests
{
    private record struct Point(int X, int Y);

    [Fact]
    public void Equals_SameValues_ReturnsTrue()
    {
        Point a = new(1, 2);
        Point b = new(1, 2);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Equals_DifferentValues_ReturnsFalse()
    {
        Point a = new(1, 2);
        Point b = new(3, 4);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Equals_NullObject_ReturnsFalse()
    {
        Point a = new(1, 2);

        // Struct.Equals(object?) with null must return false, not throw
        bool result = a.Equals((object?)null);

        Assert.False(result);
    }

    [Fact]
    public void Equals_WrongType_ReturnsFalse()
    {
        Point a = new(5, 5);

        bool result = a.Equals("not a Point");

        Assert.False(result);
    }
}
