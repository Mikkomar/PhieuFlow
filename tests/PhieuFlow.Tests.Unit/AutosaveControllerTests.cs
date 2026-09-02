using System.Diagnostics;
using System.Net.Http;
using AwesomeAssertions;
using PhieuFlow.FormBuilder.Enums;
using PhieuFlow.FormBuilder.Services;
using Xunit;

namespace PhieuFlow.Tests.Unit;

public class AutosaveControllerTests
{
    [Fact]
    public async Task TestNotifyEdited_When_GateClosed_Should_StayIdleAndNotSave()
    {
        var save = new SaveSpy();
        using var controller = new AutosaveController(save.Run, canSave: () => false, TimeSpan.FromMilliseconds(10));

        controller.NotifyEdited();
        await Task.Delay(60);

        controller.State.Should().Be(SaveState.Idle);
        save.Calls.Should().Be(0);
    }

    [Fact]
    public async Task TestNotifyEdited_When_CalledRapidly_Should_CoalesceToASingleSave()
    {
        var save = new SaveSpy();
        using var controller = new AutosaveController(save.Run, canSave: () => true, TimeSpan.FromMilliseconds(40));

        for (var i = 0; i < 5; i++)
        {
            controller.NotifyEdited();
            await Task.Delay(5);
        }

        await WaitUntil(() => controller.State == SaveState.Saved);
        save.Calls.Should().Be(1);
    }

    [Fact]
    public async Task TestFlushAsync_When_WorkPending_Should_SaveAndReportUpToDate()
    {
        // A timestamp distinct from "now" so this fails if LastSavedAt is ever seeded from the
        // client clock instead of what the save delegate returns.
        var save = new SaveSpy { SavedAt = DateTimeOffset.UtcNow.AddMinutes(-5) };
        // A long debounce so only the flush can drive the save.
        using var controller = new AutosaveController(save.Run, canSave: () => true, TimeSpan.FromSeconds(30));

        controller.NotifyEdited();
        var result = await controller.FlushAsync();

        result.Should().Be(AutosaveFlushResult.UpToDate);
        save.Calls.Should().Be(1);
        controller.HasUnsavedWork.Should().BeFalse();
        controller.State.Should().Be(SaveState.Saved);
        controller.LastSavedAt.Should().Be(save.SavedAt);
    }

    [Fact]
    public async Task TestFlushAsync_When_EditsOutrunEveryRetry_Should_ReportIncomplete()
    {
        AutosaveController controller = null!;
        var save = new SaveSpy
        {
            // Every save is immediately followed by a fresh edit, so the flush can never catch up.
            Behavior = () =>
            {
                controller.NotifyEdited();
                return Task.CompletedTask;
            },
        };
        controller = new AutosaveController(save.Run, canSave: () => true, TimeSpan.FromSeconds(30));

        using (controller)
        {
            controller.NotifyEdited();
            var result = await controller.FlushAsync();

            result.Should().Be(AutosaveFlushResult.Incomplete);
            controller.HasUnsavedWork.Should().BeTrue();
        }
    }

    [Fact]
    public async Task TestFlushAsync_When_GateClosed_Should_ReportBlockedWithoutSaving()
    {
        var save = new SaveSpy();
        using var controller = new AutosaveController(save.Run, canSave: () => false, TimeSpan.FromSeconds(30));

        controller.NotifyEdited();
        var result = await controller.FlushAsync();

        result.Should().Be(AutosaveFlushResult.Blocked);
        save.Calls.Should().Be(0);
    }

    [Fact]
    public async Task TestSave_When_EditArrivesMidSave_Should_StayPendingThenSaveAgain()
    {
        var release = new TaskCompletionSource();
        var save = new SaveSpy { Behavior = () => release.Task };
        using var controller = new AutosaveController(save.Run, canSave: () => true, TimeSpan.FromMilliseconds(20));

        controller.NotifyEdited();
        await WaitUntil(() => controller.State == SaveState.Saving);

        controller.NotifyEdited(); // lands while the first save is in flight
        release.SetResult();

        await WaitUntil(() => save.Calls == 2);
        await WaitUntil(() => controller.State == SaveState.Saved);
        controller.HasUnsavedWork.Should().BeFalse();
    }

    [Fact]
    public async Task TestSave_When_RequestFails_Should_EnterErrorAndFlushReportsFailed()
    {
        var save = new SaveSpy { Behavior = () => Task.FromException(new HttpRequestException("hub down")) };
        using var controller = new AutosaveController(save.Run, canSave: () => true, TimeSpan.FromSeconds(30));

        controller.NotifyEdited();
        var result = await controller.FlushAsync();

        result.Should().Be(AutosaveFlushResult.Failed);
        controller.State.Should().Be(SaveState.Error);
    }

    [Fact]
    public void TestSeedSaved_Should_ReportSavedWithNoUnsavedWork()
    {
        using var controller = new AutosaveController(_ => Task.FromResult(DateTimeOffset.UtcNow), canSave: () => true, TimeSpan.FromSeconds(30));

        controller.SeedSaved(DateTimeOffset.Now);

        controller.State.Should().Be(SaveState.Saved);
        controller.HasUnsavedWork.Should().BeFalse();
        controller.LastSavedAt.Should().NotBeNull();
    }

    private static async Task WaitUntil(Func<bool> condition, int timeoutMs = 3000)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!condition())
        {
            if (stopwatch.ElapsedMilliseconds > timeoutMs)
            {
                throw new TimeoutException("Condition was not met within the timeout.");
            }

            await Task.Delay(15);
        }
    }

    private sealed class SaveSpy
    {
        public int Calls { get; private set; }

        public Func<Task>? Behavior { get; init; }

        public DateTimeOffset SavedAt { get; init; } = DateTimeOffset.UtcNow;

        public async Task<DateTimeOffset> Run(CancellationToken _)
        {
            Calls++;
            if (Behavior is not null)
            {
                await Behavior.Invoke();
            }

            return SavedAt;
        }
    }
}
