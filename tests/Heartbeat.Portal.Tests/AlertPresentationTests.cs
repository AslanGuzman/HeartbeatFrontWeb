using System.Reflection;
using Heartbeat.Portal.Components.Pages;
using Heartbeat.Shared;

namespace Heartbeat.Portal.Tests;

[TestClass]
public sealed class AlertPresentationTests
{
    [TestMethod]
    [DataRow(AlertKind.Environment, "Ambiente de la misión")]
    [DataRow(AlertKind.Radiation, "Exposición a radiación")]
    [DataRow(AlertKind.CheckUp, "Revisión de salud")]
    public void AlertWithoutIndicatorsHasADescriptiveTitle(AlertKind kind, string expected)
    {
        Assert.AreEqual(expected, Title(kind, []));
    }

    [TestMethod]
    public void AlertTitlePreservesItsIndicators()
    {
        Assert.AreEqual("Blood pressure, Heart rate",
            Title(AlertKind.CheckUp, [IndicatorType.BloodPressure, IndicatorType.HeartRate]));
    }

    private static string Title(AlertKind kind, IReadOnlyList<IndicatorType> indicators)
    {
        var alert = new AlertDto(Guid.NewGuid(), Guid.NewGuid(), "Crew member", null,
            indicators, DateTimeOffset.UtcNow, null, null, null, kind);
        var method = typeof(Alerts).GetMethod("AlertTitle", BindingFlags.Static | BindingFlags.NonPublic)!;
        return (string)method.Invoke(null, [alert])!;
    }
}
