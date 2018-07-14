/*
' Copyright (c) 2017 Risdall Marketing Group
'  All rights reserved.
' 
' THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED
' TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
' THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF
' CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
' DEALINGS IN THE SOFTWARE.
' 
*/

using System;
using System.Collections.Generic;
//using System.Xml;
using DotNetNuke.Entities.Modules;
using DotNetNuke.Services.Scheduling;
using DotNetNuke.Services.Search;
using DotNetNuke.Services.Social.Notifications;

namespace Risdall.Modules.DNN_AMP.Components
{

    /// -----------------------------------------------------------------------------
    /// <summary>
    /// The Controller class for DNN_AMP
    /// 
    /// The FeatureController class is defined as the BusinessController in the manifest file (.dnn)
    /// DotNetNuke will poll this class to find out which Interfaces the class implements. 
    /// 
    /// The IPortable interface is used to import/export content from a DNN module
    /// 
    /// The ISearchable interface is used by DNN to index the content of a module
    /// 
    /// The IUpgradeable interface allows module developers to execute code during the upgrade 
    /// process for a module.
    /// 
    /// Below you will find stubbed out implementations of each, uncomment and populate with your own data
    /// </summary>
    /// -----------------------------------------------------------------------------

    //uncomment the interfaces to add the support.
    public class FeatureController : IUpgradeable //: IPortable, ISearchable, IUpgradeable
    {
        
        #region Optional Interfaces

        public string UpgradeModule(string version)
        {
            if (version == "00.00.02" || version == "0.0.2")
            {
                // add in scheduled task
                SetScheduledTask("Risdall.Modules.DNN_AMP.ScheduledTasks.BuildAmps,DotNetNuke.DNN_AMP");
            }

            return version;
        }

        #endregion

        #region Helpers
        private void SetScheduledTask(string typefullname)
        {
            var t = SchedulingProvider.Instance().GetSchedule(typefullname, string.Empty);

            if (t != null)
                return;
            
            var i = new ScheduleItem();
            i.CatchUpEnabled = false;
            i.Enabled = true;
            i.NextStart = DateTime.Now.AddMinutes(1);
            i.RetainHistoryNum = 50;
            i.TypeFullName = typefullname;
            i.ScheduleSource = ScheduleSource.NOT_SET;

            // set custom settings
            i.FriendlyName = "AMP Runner";
            i.TimeLapse = 1;
            i.TimeLapseMeasurement = "d";
            i.RetryTimeLapse = 1;
            i.RetryTimeLapseMeasurement = "h";

            SchedulingProvider.Instance().AddSchedule(i);
        }
        #endregion

    }
}