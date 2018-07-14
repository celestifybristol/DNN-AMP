using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web;
using DotNetNuke.ComponentModel;
using DotNetNuke.Entities.Controllers;
using DotNetNuke.Entities.Portals;
using DotNetNuke.Entities.Tabs;
using DotNetNuke.Services.Exceptions;
using DotNetNuke.Services.Localization;
using DotNetNuke.Services.Scheduling;
using DotNetNuke.Services.Sitemap;
using Risdall.Modules.DNN_AMP.Components;

namespace Risdall.Modules.DNN_AMP.ScheduledTasks
{
    public class BuildAmps : SchedulerClient
    {
        

        public BuildAmps(ScheduleHistoryItem shItem) : base()
        {
            ScheduleHistoryItem = shItem;
        }

        public override void DoWork()
        {
            try
            {
                GoogleAmpConverter.BuildAmps();

                // report success to the scheduler framework
                ScheduleHistoryItem.Succeeded = true;
            }
            catch (Exception exc)
            {
                ScheduleHistoryItem.Succeeded = false;
                ScheduleHistoryItem.AddLogNote("EXCEPTION: " + exc.ToString());
                Errored(ref exc);
                Exceptions.LogException(exc);
            }
        }
    }
}