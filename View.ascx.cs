/*
' Copyright (c) 2017  Risdall Marketing Group
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
using DotNetNuke.Entities.Modules;
using DotNetNuke.Services.FileSystem;
using DotNetNuke.Common.Utilities;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Web.UI.HtmlControls;
using System.Collections.Generic;
using System.Threading.Tasks;
using HtmlAgilityPack;
using System.IO;
using System.Text;
using System.Net;
using System.Linq;
using DotNetNuke.Entities.Portals;
using Risdall.Modules.DNN_AMP.Components;

namespace Risdall.Modules.DNN_AMP
{
    public partial class View : PortalModuleBase
    {

        #region Event Handlers
        
        protected void cmdSave_Click(object sender, EventArgs e)
        {
            //Get current page URL
            var url = HttpContext.Current.Request.Url.Scheme + "://" + HttpContext.Current.Request.Url.Authority + HttpContext.Current.Request.RawUrl;
            GoogleAmpConverter.ConvertUrl(url, PortalSettings.Current);
        }

        #endregion

    }
}