using System;
using System.Collections.Generic;

namespace LicenseSeat.Tests;

public class EventBusTests
{
    [Fact]
    public void On_WithValidParameters_ReturnsSubscription()
    {
        var bus = new EventBus();
        var received = false;

        var subscription = bus.On("test", _ => received = true);

        Assert.NotNull(subscription);
        bus.Emit("test");
        Assert.True(received);
    }

    [Fact]
    public void On_WithNullEventName_ThrowsArgumentNullException()
    {
        var bus = new EventBus();

        Assert.Throws<ArgumentNullException>(() => bus.On(null!, _ => { }));
    }

    [Fact]
    public void On_WithNullHandler_ThrowsArgumentNullException()
    {
        var bus = new EventBus();

        Assert.Throws<ArgumentNullException>(() => bus.On("test", (Action<object?>)null!));
    }

    [Fact]
    public void On_MultipleHandlers_AllReceiveEvents()
    {
        var bus = new EventBus();
        var receivedCount = 0;

        bus.On("test", _ => receivedCount++);
        bus.On("test", _ => receivedCount++);
        bus.On("test", _ => receivedCount++);

        bus.Emit("test");

        Assert.Equal(3, receivedCount);
    }

    [Fact]
    public void On_DifferentEvents_OnlyReceiveOwnEvents()
    {
        var bus = new EventBus();
        var event1Received = false;
        var event2Received = false;

        bus.On("event1", _ => event1Received = true);
        bus.On("event2", _ => event2Received = true);

        bus.Emit("event1");

        Assert.True(event1Received);
        Assert.False(event2Received);
    }

    [Fact]
    public void On_TypedHandler_ReceivesTypedData()
    {
        var bus = new EventBus();
        License? received = null;

        bus.On<License>("test", license => received = license);

        var license = new License { LicenseKey = "test-key" };
        bus.Emit("test", license);

        Assert.NotNull(received);
        Assert.Equal("test-key", received.LicenseKey);
    }

    [Fact]
    public void On_TypedHandler_IgnoresWrongType()
    {
        var bus = new EventBus();
        License? received = null;

        bus.On<License>("test", license => received = license);

        bus.Emit("test", "not a license");

        Assert.Null(received);
    }

    [Fact]
    public void Emit_WithData_PassesDataToHandler()
    {
        var bus = new EventBus();
        object? receivedData = null;

        bus.On("test", data => receivedData = data);

        var testData = new { Message = "Hello" };
        bus.Emit("test", testData);

        Assert.Same(testData, receivedData);
    }

    [Fact]
    public void Emit_WithNullData_PassesNull()
    {
        var bus = new EventBus();
        var wasInvoked = false;
        object? receivedData = "not null";

        bus.On("test", data =>
        {
            wasInvoked = true;
            receivedData = data;
        });

        bus.Emit("test", null);

        Assert.True(wasInvoked);
        Assert.Null(receivedData);
    }

    [Fact]
    public void Emit_WithNullEventName_ThrowsArgumentNullException()
    {
        var bus = new EventBus();

        Assert.Throws<ArgumentNullException>(() => bus.Emit(null!));
    }

    [Fact]
    public void Emit_WithNoSubscribers_DoesNotThrow()
    {
        var bus = new EventBus();

        var exception = Record.Exception(() => bus.Emit("unsubscribed-event"));

        Assert.Null(exception);
    }

    [Fact]
    public void Emit_WhenHandlerThrows_ContinuesWithOtherHandlers()
    {
        var bus = new EventBus();
        var handler2Called = false;

        bus.On("test", _ => throw new InvalidOperationException("Handler error"));
        bus.On("test", _ => handler2Called = true);

        var exception = Record.Exception(() => bus.Emit("test"));

        Assert.Null(exception);
        Assert.True(handler2Called);
    }

    [Fact]
    public void Off_RemovesHandler()
    {
        var bus = new EventBus();
        var callCount = 0;
        Action<object?> handler = _ => callCount++;

        bus.On("test", handler);
        bus.Emit("test");
        Assert.Equal(1, callCount);

        bus.Off("test", handler);
        bus.Emit("test");
        Assert.Equal(1, callCount); // Still 1, handler was removed
    }

    [Fact]
    public void Dispose_Subscription_Unsubscribes()
    {
        var bus = new EventBus();
        var callCount = 0;

        var subscription = bus.On("test", _ => callCount++);
        bus.Emit("test");
        Assert.Equal(1, callCount);

        subscription.Dispose();
        bus.Emit("test");
        Assert.Equal(1, callCount); // Still 1, handler was removed
    }

    [Fact]
    public void Dispose_MultipleTimes_DoesNotThrow()
    {
        var bus = new EventBus();
        var subscription = bus.On("test", _ => { });

        subscription.Dispose();
        var exception = Record.Exception(() => subscription.Dispose());

        Assert.Null(exception);
    }

    [Fact]
    public void Clear_RemovesAllSubscriptions()
    {
        var bus = new EventBus();
        var callCount = 0;

        bus.On("event1", _ => callCount++);
        bus.On("event2", _ => callCount++);

        bus.Clear();

        bus.Emit("event1");
        bus.Emit("event2");

        Assert.Equal(0, callCount);
    }

    [Fact]
    public void Clear_WithEventName_RemovesOnlyThatEvent()
    {
        var bus = new EventBus();
        var event1Count = 0;
        var event2Count = 0;

        bus.On("event1", _ => event1Count++);
        bus.On("event2", _ => event2Count++);

        bus.Clear("event1");

        bus.Emit("event1");
        bus.Emit("event2");

        Assert.Equal(0, event1Count);
        Assert.Equal(1, event2Count);
    }

    [Fact]
    public void GetSubscriberCount_ReturnsCorrectCount()
    {
        var bus = new EventBus();

        Assert.Equal(0, bus.GetSubscriberCount("test"));

        bus.On("test", _ => { });
        Assert.Equal(1, bus.GetSubscriberCount("test"));

        bus.On("test", _ => { });
        Assert.Equal(2, bus.GetSubscriberCount("test"));

        bus.Clear("test");
        Assert.Equal(0, bus.GetSubscriberCount("test"));
    }

    [Fact]
    public void EventConstants_AreCorrect()
    {
        // Just verify some key event constants
        Assert.Equal("activation:success", LicenseSeatEvents.ActivationSuccess);
        Assert.Equal("validation:success", LicenseSeatEvents.ValidationSuccess);
        Assert.Equal("license:loaded", LicenseSeatEvents.LicenseLoaded);
        Assert.Equal("network:online", LicenseSeatEvents.NetworkOnline);
    }

    [Fact]
    public async System.Threading.Tasks.Task ThreadSafety_ConcurrentOperations_DoNotThrow()
    {
        var bus = new EventBus();
        var exceptions = new List<Exception>();

        var tasks = new List<System.Threading.Tasks.Task>();

        for (int i = 0; i < 100; i++)
        {
            tasks.Add(System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var sub = bus.On("test", _ => { });
                    bus.Emit("test", "data");
                    sub.Dispose();
                }
                catch (Exception ex)
                {
                    lock (exceptions)
                    {
                        exceptions.Add(ex);
                    }
                }
            }));
        }

        await System.Threading.Tasks.Task.WhenAll(tasks);

        Assert.Empty(exceptions);
    }
}
