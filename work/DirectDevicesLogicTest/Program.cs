using System.Reflection;
using Artemis.Plugins.DirectDevices;

Type controllerType = typeof(DirectCsController);
Type stateType = controllerType.GetNestedType("CsGameState", BindingFlags.NonPublic)
    ?? throw new InvalidOperationException("CS game state type was not found.");
MethodInfo updateMvpState = controllerType.GetMethod("UpdateMvpState", BindingFlags.Static | BindingFlags.NonPublic)
    ?? throw new InvalidOperationException("MVP update method was not found.");
PropertyInfo lastMvpUtc = stateType.GetProperty("LastMvpUtc")
    ?? throw new InvalidOperationException("MVP timestamp property was not found.");
PropertyInfo mvps = stateType.GetProperty("Mvps")
    ?? throw new InvalidOperationException("MVP count property was not found.");

object state = Activator.CreateInstance(stateType, nonPublic: true)
    ?? throw new InvalidOperationException("CS game state could not be created.");
DateTime firstEvent = new(2026, 6, 12, 12, 0, 0, DateTimeKind.Utc);
DateTime duplicateEvent = firstEvent.AddSeconds(1);

updateMvpState.Invoke(null, [state, 2, firstEvent]);
Require((DateTime)lastMvpUtc.GetValue(state)! == DateTime.MinValue, "Initial MVP count must establish a baseline.");

updateMvpState.Invoke(null, [state, 3, firstEvent]);
Require((DateTime)lastMvpUtc.GetValue(state)! == firstEvent, "An increased MVP count must trigger the event.");
Require((int?)mvps.GetValue(state) == 3, "The new MVP count must be stored.");

updateMvpState.Invoke(null, [state, 3, duplicateEvent]);
Require((DateTime)lastMvpUtc.GetValue(state)! == firstEvent, "A repeated MVP count must not retrigger.");

updateMvpState.Invoke(null, [state, 0, duplicateEvent]);
Require((DateTime)lastMvpUtc.GetValue(state)! == firstEvent, "A new-match reset must not trigger.");
Require((int?)mvps.GetValue(state) == 0, "A new-match reset must become the next baseline.");

Console.WriteLine("MVP_LOGIC_TEST_OK");

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
