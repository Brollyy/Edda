using Avalonia;
using Avalonia.Automation;

namespace Edda.Avalonia;

internal static class AutomationHelper {
    public static T WithAutomationId<T>(T element, string automationId) where T : StyledElement {
        AutomationProperties.SetAutomationId(element, automationId);
        return element;
    }

    public static void SetAutomationId(StyledElement element, string automationId) {
        AutomationProperties.SetAutomationId(element, automationId);
    }

    public static void SetHelpText(StyledElement element, string helpText) {
        AutomationProperties.SetHelpText(element, helpText);
    }

    public static void SetItemStatus(StyledElement element, string itemStatus) {
        AutomationProperties.SetItemStatus(element, itemStatus);
    }
}