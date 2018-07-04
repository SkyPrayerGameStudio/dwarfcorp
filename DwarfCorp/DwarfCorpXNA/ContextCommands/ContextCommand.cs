﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DwarfCorp.ContextCommands
{
    public class ContextCommand
    {
        public string Name;
        public string Description = "";
        public Gui.TileReference Icon;

        public virtual bool CanBeAppliedTo(Body Entity, WorldManager World)
        {
            return false;
        }

        public virtual void Apply(Body Entity, WorldManager World)
        {

        }
    }
}
