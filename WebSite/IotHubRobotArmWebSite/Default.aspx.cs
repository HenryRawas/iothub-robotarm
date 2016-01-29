//  ---------------------------------------------------------------------------------
//  Copyright (c) Microsoft Open Technologies, Inc.  All rights reserved.
// 
//  The MIT License (MIT)
// 
//  Permission is hereby granted, free of charge, to any person obtaining a copy
//  of this software and associated documentation files (the "Software"), to deal
//  in the Software without restriction, including without limitation the rights
//  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//  copies of the Software, and to permit persons to whom the Software is
//  furnished to do so, subject to the following conditions:
// 
//  The above copyright notice and this permission notice shall be included in
//  all copies or substantial portions of the Software.
// 
//  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
//  THE SOFTWARE.
//  ---------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Threading.Tasks;

namespace IotHubRobotArmWebSite
{
    public partial class Default : System.Web.UI.Page
    {
        protected string ForceSocketCloseOnUserActionsTimeout = "false";
        private string devId;
        private string cmd;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                ForceSocketCloseOnUserActionsTimeout =
                    Global.globalSettings.ForceSocketCloseOnUserActionsTimeout.ToString();
            }
        }

        protected void OnClickCommand(object sender, EventArgs e)
        {
            devId = Global.globalSettings.deviceId;

            cmd = null;

            if (sender == pause)
            {
                cmd = "pause";
            }
            else if (sender == resume)
            {
                cmd = "resume";
            }
            else if (sender == runupdown)
            {
                cmd = "runupdown";
            }
            else if (sender == runuptwist)
            {
                cmd = "runuptwist";
            }
            else if (sender == runhome)
            {
                cmd = "runhome";
            }
            else if (sender == runwave)
            {
                cmd = "runwave";
            }
            else if (sender == runtaps1)
            {
                cmd = "runtaps1";
            }
            else if (sender == fastwave)
            {
                cmd = "fastwave";
            }
            else if (sender == cancel)
            {
                cmd = "cancel";
            }
            else
                return;

            DeviceCommands devcmd = new DeviceCommands();
            devcmd.Init(devId, cmd);
            RegisterAsyncTask(new PageAsyncTask(devcmd.SendMessageToDeviceAsync));
        }

    }
}