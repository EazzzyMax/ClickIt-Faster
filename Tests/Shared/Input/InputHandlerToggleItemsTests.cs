namespace ClickItMax.Tests.Shared.Input
{
    [TestClass]
    public class InputHandlerToggleItemsTests
    {
        [TestMethod]
        public void PeriodicToggle_WaitsFullIntervalAndSendsExactlyTwoConfiguredKeys()
        {
            long now = 1000;
            var settings = new ClickItMaxSettings();
            settings.ToggleItems.Value = true;
            settings.ToggleItemsIntervalMs.Value = 1500;
            settings.ToggleItemsHotkey = new HotkeyNodeV2(Keys.Z);
            var keys = new List<(Keys Key, int Delay)>();
            var controller = new ToggleItemsController(settings, (key, delay) => keys.Add((key, delay)), () => now);
            controller.BeginPeriodicSession();

            controller.TriggerPeriodicToggleItems().Should().BeFalse("no startup flicker");
            now = 2499;
            controller.TriggerPeriodicToggleItems().Should().BeFalse("full interval has not elapsed");
            keys.Should().BeEmpty();
            now = 2500;
            controller.TriggerPeriodicToggleItems().Should().BeTrue();
            keys.Should().Equal((Keys.Z, 20), (Keys.Z, 20));
            controller.TriggerPeriodicToggleItems().Should().BeFalse("no duplicate pair in the same tick");
            now = 4000;
            controller.TriggerPeriodicToggleItems().Should().BeTrue();
            keys.Count.Should().Be(4);
        }

        [TestMethod]
        public void PeriodicToggle_ReleaseAndNewSessionRestartTheFullInterval()
        {
            long now = 1000;
            var settings = new ClickItMaxSettings();
            settings.ToggleItems.Value = true;
            settings.ToggleItemsIntervalMs.Value = 1500;
            int presses = 0;
            var controller = new ToggleItemsController(settings, (_, _) => presses++, () => now);
            controller.BeginPeriodicSession();
            now = 2000;
            controller.EndPeriodicSession();
            now = 5000;
            controller.TriggerPeriodicToggleItems().Should().BeFalse("no inputs after release");
            controller.BeginPeriodicSession();
            controller.TriggerPeriodicToggleItems().Should().BeFalse("new activation must not inherit an expired timer");
            now = 6499;
            controller.TriggerPeriodicToggleItems().Should().BeFalse();
            now = 6500;
            controller.TriggerPeriodicToggleItems().Should().BeTrue();
            presses.Should().Be(2);
        }

        [TestMethod]
        public void PeriodicToggle_LongGapSendsOnePairWithoutCatchUpBurst()
        {
            long now = 1000;
            var settings = new ClickItMaxSettings();
            settings.ToggleItems.Value = true;
            settings.ToggleItemsIntervalMs.Value = 1500;
            int presses = 0;
            var controller = new ToggleItemsController(settings, (_, _) => presses++, () => now);
            controller.BeginPeriodicSession();
            now = 10000;
            controller.IsPeriodicToggleDue().Should().BeTrue();
            presses.Should().Be(0, "checking due never sends native input");
            controller.TriggerPeriodicToggleItems().Should().BeTrue();
            controller.TriggerPeriodicToggleItems().Should().BeFalse();
            presses.Should().Be(2);
            now = 11499;
            controller.TriggerPeriodicToggleItems().Should().BeFalse();
            now = 11500;
            controller.TriggerPeriodicToggleItems().Should().BeTrue();
        }

        [TestMethod]
        public void PeriodicToggle_EnablingMidSessionDoesNotSendImmediately()
        {
            long now = 1000;
            var settings = new ClickItMaxSettings();
            settings.ToggleItems.Value = false;
            settings.ToggleItemsIntervalMs.Value = 1500;
            int presses = 0;
            var controller = new ToggleItemsController(settings, (_, _) => presses++, () => now);
            controller.BeginPeriodicSession();
            now = 5000;
            controller.TriggerPeriodicToggleItems().Should().BeFalse();
            settings.ToggleItems.Value = true;
            controller.TriggerPeriodicToggleItems().Should().BeFalse("start the timer when enabling");
            now = 6500;
            controller.TriggerPeriodicToggleItems().Should().BeTrue();
            presses.Should().Be(2);
            settings.ToggleItems.Value = false;
            now = 8000;
            controller.TriggerPeriodicToggleItems().Should().BeFalse();
        }

        [TestMethod]
        public void PeriodicToggle_PostBlockStartsAfterBothKeyPressesFinish()
        {
            long now = 1000;
            var settings = new ClickItMaxSettings();
            settings.ToggleItems.Value = true;
            settings.ToggleItemsIntervalMs.Value = 1500;
            settings.ToggleItemsPostToggleClickBlockMs.Value = 20;
            var controller = new ToggleItemsController(settings, (_, delay) => now += delay, () => now);
            controller.BeginPeriodicSession();
            now = 2500;
            controller.TriggerPeriodicToggleItems().Should().BeTrue();
            now.Should().Be(2540);
            controller.IsInPostClickBlockWindow().Should().BeTrue();
            now = 2559;
            controller.IsInPostClickBlockWindow().Should().BeTrue();
            now = 2560;
            controller.IsInPostClickBlockWindow().Should().BeFalse();
        }

        [TestMethod]
        public void PeriodicToggle_RuntimeIntervalChangesAreRespected()
        {
            long now = 1000;
            var settings = new ClickItMaxSettings();
            settings.ToggleItems.Value = true;
            settings.ToggleItemsIntervalMs.Value = 1500;
            var controller = new ToggleItemsController(settings, static (_, _) => { }, () => now);
            controller.BeginPeriodicSession();
            now = 2500;
            settings.ToggleItemsIntervalMs.Value = 3000;
            controller.TriggerPeriodicToggleItems().Should().BeFalse();
            now = 4000;
            controller.TriggerPeriodicToggleItems().Should().BeTrue();
            settings.ToggleItemsIntervalMs.Value = 500;
            now = 4500;
            controller.TriggerPeriodicToggleItems().Should().BeTrue();
        }

        [TestMethod]
        public void GetToggleItemsPostClickBlockMs_ClampsNegativeValueToZero()
        {
            var settings = new ClickItMaxSettings();
            settings.ToggleItemsPostToggleClickBlockMs.Value = -25;
            var controller = new ToggleItemsController(settings, static (_, _) => { });

            controller.GetToggleItemsPostClickBlockMs().Should().Be(0);
        }

        [TestMethod]
        public void IsInToggleItemsPostClickBlockWindow_ReturnsTrue_WhenInsideBlockWindow()
        {
            var settings = new ClickItMaxSettings();
            settings.ToggleItemsPostToggleClickBlockMs.Value = 500;
            var controller = new ToggleItemsController(settings, static (_, _) => { });

            long now = Environment.TickCount64;
            controller.SetLastToggleItemsTimestamp(now - 50L);

            bool inWindow = controller.IsInPostClickBlockWindow();

            inWindow.Should().BeTrue();
        }

        [TestMethod]
        public void IsInToggleItemsPostClickBlockWindow_ReturnsFalse_WhenTimestampIsInFuture()
        {
            var settings = new ClickItMaxSettings();
            settings.ToggleItemsPostToggleClickBlockMs.Value = 500;
            var controller = new ToggleItemsController(settings, static (_, _) => { });

            long now = Environment.TickCount64;
            controller.SetLastToggleItemsTimestamp(now + 100L);

            bool inWindow = controller.IsInPostClickBlockWindow();

            inWindow.Should().BeFalse();
        }

        [TestMethod]
        public void TriggerToggleItems_ReturnsFalse_WhenDisabled()
        {
            var settings = new ClickItMaxSettings();
            settings.ToggleItems.Value = false;
            var controller = new ToggleItemsController(settings, static (_, _) => { });

            bool triggered = controller.TriggerToggleItems();

            triggered.Should().BeFalse();
        }

        [TestMethod]
        public void TriggerToggleItems_ReturnsFalse_WhenIntervalHasNotElapsed()
        {
            var settings = new ClickItMaxSettings();
            settings.ToggleItems.Value = true;
            settings.ToggleItemsIntervalMs.Value = 1000;
            var controller = new ToggleItemsController(settings, static (_, _) => { });

            controller.SetLastToggleItemsTimestamp(Environment.TickCount64);

            bool triggered = controller.TriggerToggleItems();

            triggered.Should().BeFalse();
        }
    }
}
