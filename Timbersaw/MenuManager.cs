﻿namespace Timbersaw
{
    using Ensage.Common.Menu;

    internal class MenuManager
    {
        #region Fields

        private readonly MenuItem centerHero;

        private readonly MenuItem enabled;

        private readonly Menu menu;

        private readonly MenuItem safeChain;

        #endregion

        #region Constructors and Destructors

        public MenuManager(string heroName)
        {
            menu = new Menu("Timbersaw ?", "timbersawQuestionMark", true, heroName, true);

            menu.AddItem(enabled = new MenuItem("enabled", "Enabled").SetValue(true));
            menu.AddItem(safeChain = new MenuItem("safeChain", "Safecast chain").SetValue(true))
                .SetTooltip("Will prevent chain cast if it wont hit tree when used manually");
            menu.AddItem(centerHero = new MenuItem("centerHero", "Center camera").SetValue(false))
                .SetTooltip("Center camera on timbersaw when chase enabled");
            menu.AddItem(new MenuItem("comboKey", "Chase").SetValue(new KeyBind('F', KeyBindType.Press))).ValueChanged
                += (sender, arg) => { ChaseEnabled = arg.GetNewValue<KeyBind>().Active; };
            menu.AddItem(new MenuItem("escapeKey", "Escape").SetValue(new KeyBind('G', KeyBindType.Press))).ValueChanged
                += (sender, arg) => { EscapeEnabled = arg.GetNewValue<KeyBind>().Active; };

            menu.AddToMainMenu();
        }

        #endregion

        #region Public Properties

        public bool ChaseEnabled { get; private set; }

        public bool EscapeEnabled { get; private set; }

        public bool IsCenterCameraEnabled => centerHero.GetValue<bool>();

        public bool IsEnabled => enabled.GetValue<bool>();

        public bool IsSafeChainEnabled => safeChain.GetValue<bool>();

        #endregion

        #region Public Methods and Operators

        public void OnClose()
        {
            menu.RemoveFromMainMenu();
        }

        #endregion
    }
}