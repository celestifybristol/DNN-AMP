using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Web.UI.HtmlControls;

namespace Risdall.Modules.DNN_AMP.SkinObjects
{
    public partial class AmpMeta1 : DotNetNuke.UI.Skins.SkinObjectBase
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            // skip if home page
            if (Request.RawUrl == "/")
                return;

            // get amp meta tag
            // <link rel="amphtml" href="https://www.example.com/url/to/amp/document.html">
            var url = $"{Request.Url.Scheme}://{Request.Url.Authority}/portals/{PortalSettings.PortalId}/amp{Request.RawUrl}.html";                

            var meta = new HtmlMeta();
            meta.Name = "amphtml";
            meta.Content = url;
            Page.Header.Controls.Add(meta);
        }
    }
}