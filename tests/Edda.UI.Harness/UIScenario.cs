namespace Edda.UI.Harness {
    public abstract class UIScenario {
        public abstract string Name { get; }

        public abstract void Run(IUIDriver driver);
    }
}