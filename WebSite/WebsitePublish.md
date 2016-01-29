# Website Publish #
This document explains how to build and deploy a sample website that is used to show data and alerts in the IoT hub Robot Arm project. It assumes you have all necessary software and subscriptions and that you have cloned or download the iothub-robotarm project on your machine.

The website can be run on your own computer, or it can be deployed to run in Azure.

The website code also includes code that invokes the Azure Anomaly Detection service, and converts any anomaly into an alert.

## Prerequisites ##

Make sure you have all software installed and necessary subscriptions as indicated in the Readme.md file for the project. You will need several of the strings that you were asked to take note of in the Readme.


## Configure Web.config
In order for the website code to work, you must edit the `web.config` file with values that match your setup.

To do this, open `WebSite\IotHubRobotArmWebSite\web.config` and find the **appSettings** section

  - for key **Microsoft.ServiceBus.EventHubDevices**, set the value to the string you noted for your IoT hub **Event Hub-compatible name**.
  - for key **Microsoft.ServiceBus.ConnectionStringDevices**, set the value to the string you noted for your IoT hub **Event Hub-compatible endpoint**.
  - for key **Microsoft.ServiceBus.ConnectionString**, set the value to the string you noted for your IoT hub **Event Hub-compatible endpoint**.
  - for key **Microsoft.ServiceBus.EventHubAlerts**, set the value to the string you noted for your Event hub **Event hub name**.
  - for key **Microsoft.ServiceBus.ConnectionStringAlerts**, set the value to the string you noted for your Event hub **Connection information**.
  - for key **Microsoft.Storage.ConnectionString**, set the value to the string you noted for your Storage account **Primary connection string**.
  - for key **DeviceId**, set the value to the string you noted when you created your device identity **Device Id**.
  - for key **AnomalyDetectionAuthKey**, set the value to the string you noted for your Anomaly detection account **Primary Account Key**.
  - for key **LiveId**, set the value to the string you noted for your Anomaly detection account **Account email**.
 

## Publish the Azure Website ##
Note that for development you can run the website locally, and do not need to publish it to Azure.

* Open the `WebSite\IotHubRobotArmWebSite.sln` solution in Visual Studio
* Make sure the `web.config` file has been updated
* In VS, Right-click on the project name and select *Publish*.
* Select Azure Web Sites, create new one. 
    * Site name: [pick something unique]
    * Region: [pick same region as you used your IoT hub and Event Hub]
    * Database server: no database
    * Database password: [leave suggested password]
* Publish (you might need to install WebDeploy extension if you are having an error stating that the Web deployment task failed. You can find WebDeploy [here](http://www.iis.net/downloads/microsoft/web-deploy)).

## Websockets setting ##
* Enable WebSockets for the new Azure Web site
    * Browse to https://manage.windowsazure.com and select your Azure Web Site.
    * Click on the Configure tab. Then set WebSockets to On and Click "Save"
	
## Running the site
* Open the site in a browser to verify it has deployed correctly. 
    * At the bottom of the page you should see “Connected.”. If you see “ERROR undefined” you likely didn’t enable WebSockets for the Azure Web Site (see above section).

**Note** There is a chance you won't see any data coming into your site when you first start it up. If your device is not running and connected to the internet, then there will be no data. If you device is running and sending messages, try rebooting your gateway.

