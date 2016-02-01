---
services: iot-hub, anomaly detection
platforms: mbed, c, cpp, freescale k64f
author: olivierbloch
---

# Monitoring and controlling a robot arm using Azure IoT services on data from a Freescale K64F running code developed for mbed
If you want to try out Azure IoT hub and other Azure IoT services, this sample will walk you through the configuration of services and the setup of a simple robot arm built using Dynamixel AX-12A actuators.

The device will send a steady stream of measurement data from the actuators to IoT hub. If some data value exceeds a local threshold, such as a high temperature or high load, the device will pause and send an alert to IoT hub. The device will also listen for commands coming from IoT hub, and act of the command.

A web server will display the device data using charts, show alerts in a table, and provide a UI for sending commands to the device.

The web server also uses the Azure Anomaly Detection service to detect unexpected patterns in the temperature data and  will generate an alert. These alerts are shown on the web page and are sent to the robot arm to pause the device until the operator takes action.

## Running this sample
### Hardware prerequisites
In order to run this sample you will need the following hardware:

  - [mbed-enabled Freescale FRDM-K64F](https://developer.mbed.org/platforms/FRDM-K64F/)
  - [mbed Application shield](https://developer.mbed.org/components/mbed-Application-Shield/)
  - Both can be found together in the [mbed Ethernet IoT Started Kit](https://developer.mbed.org/platforms/IBMethernetKit/)
  - 5 [Dynamixel AX-12A](http://en.robotis.com/index/product.php?cate_code=101010) actuators.
  - Some additional components:
    - 12V power supply
    - Buzzer
    - A base for mounting the components

### Software prerequisites
  - [Visual Studio 2015](https://www.visualstudio.com/) with [Azure SDK for .Net](http://go.microsoft.com/fwlink/?linkid=518003&clcid=0x409)
  - A Serial terminal, such as [PuTTY], so you can monitor debug traces from the devices.
  - A developer account for [mbed](https://developer.mbed.org/) to import and build the code that runs on the robot

### Services setup
In order to run the sample you will need to do the following (directions for each step follow):

  - Create an IoT hub that will receive data from devices and send commands and notifications back to it
  - Create a subscription to the Azure Anomaly Detection service
  - Create an Event hub into which we will post alerts triggered by the Anomaly Detection service
  - Create an storage blob which will be used by the event hub.
  - Deploy a web site that will read data and alerts from the IoT hub, send commands to the IoT hub, and invoke the Anomaly Detection service.   
    - The instructions for the website setup are in website/WebsitePublish.md. Those instructions make use of several of the values that you configure in the services setup. Please make a note of values where instructed to do so.


#### Create an IoT Hub
1. Log on to the [Azure Preview Portal].

1. In the jumpbar, click **New**, then click **Internet of Things**, and then click **IoT Hub**.

1. In the **New IoT Hub** blade, specify the desired configuration for the IoT hub.
  - In the **Name** box, enter a name to identify your IoT hub such as *myiothubname*. When the **Name** is validated, a green check mark appears in the **Name** box.
  - Change the **Pricing and scale tier** as desired. This tutorial does not require a specific tier.
  - In the **Resource group** box, create a new resource group, or select and existing one. For more information, see [Using resource groups to manage your Azure resources](resource-group-portal.md).
  - Use **Location** to specify the geographic location in which to host your IoT hub.  

1. Once the new IoT hub options are configured, click **Create**.  It can take a few minutes for the IoT hub to be created.  To check the status, you can monitor the progress on the Startboard. Or, you can monitor your progress from the Notifications section.

1. After the IoT hub has been created successfully, open the blade of the new IoT hub, take note of the Hostname, and select the **Key** icon on the top.

1. Select the Shared access policy called **iothubowner**, then copy and take note of the **connection string** on the right blade.

1. From the IoT hub blade, select **Settings**, and then **Messaging**. In the Messaging blade, take note of the **Event Hub-compatible name** and and of the **Event Hub-compatible endpoint**. 

Your IoT hub is now created, and you have the Hostname, connection string, an event hub compatible name and connection string you need to complete this tutorial.

#### Sign up for Anomaly Detection service
This tutorial uses the Anomaly Detection service to check for spikes or other anomalies in the temperature data coming from the device. If you are not going to use anomaly detection, then you do not need to do this step.

1. Go to [Azure Marketplace Anomaly Detection](https://azure.microsoft.com/en-us/marketplace/partners/aml-labs/anomalydetection/).

2. Sign in with your account or create a new account, and then choose a plan. Make a note of your **Account email**.

3. Click on the **My Account** tab. Make a note of the **Primary Account Key**.


#### Create an Event Hub
This tutorial uses an event hub to pass alert messages from the code that calls the Anomaly Detection service back to the main event processing code in the web server. If you are not going to use anomaly detection, then you do not need to do this step.

1. Log on to the [Azure Preview Portal].

1. In the jumpbar, click **New**, then click **Data and Analytics**, and then click **Event Hub**.

1. A new browser window will open in the Microsoft Azure Manager, where you can configure the event hub.

1. Enter an **Event hub name**, select a region, and select a namespace. Then click on **Create a new event hub**.

1. You will see a list of event hubs for your namespace. Select the one you created, and then click on the **Configure** tab. In the **shared access policies** section enter a policy name such as "readwrite", and in the permission area select Manage, Send, and Listen. Click **Save**.

1. Click on the **Dashboard** tab, then click on **Connection information**. Copy and make a note of the Connection string and of the event hub name.

#### Create a storage account
1. Log on to the [Azure Preview Portal].

1. In the jumpbar, click **New** and select **Data + Storage** then **Storage Account**

1. Choose **Classic** for the deployment model and click on **create**

1. Enter the name of your choice (i.e. "*mystorageaccountname*" for the account name and select your resource group, subscription,... then click on "Create"

1. Once the account is created, find it in the resources blade, click on Settings and then keys and make a note of the **Primary connection string** for it to configure the worker role



#### Create a new device identity in the IoT Hub
To connect your device to the IoT hub instance, you need to generate a unique identity and connection string. IoT hub does that for you.
To create a new device identity, you have the following options:

- Use the [Device Explorer tool][device-explorer] (runs only on Windows for now)
- Use the node.js tool
  - For this one, you need to have node installed on your machine (https://nodejs.org/en/)
  - Once node is installed, in a command shell, type the following commands:

    ```
    npm install -g iothub-explorer
    ```

  - You can type the following to learn more about the tool
  
    ```
    iothub-explorer help
    ```
  
  - Type the following commands to create a new device identity (replace <connectionstring> in the commands with the **connection string** for the **iothubowner** that you retrieved previously in the portal.
  
    ```
    iothub-explorer <connectionstring> create mydevice --connection-string
    ```

  - This will create a new device identity in your IoT hub and will display the required information. Copy the device **Connection String** and the **Device Id**

### Connect the device

1. Connect the board to your network using an Ethernet cable. This step is required, as the sample depends on Internet access.

1. Plug the device into your computer using a micro-USB cable. Be sure to attach the cable to the correct USB port on the device (the CMSIS-DAP USB one, see [here](https://developer.mbed.org/platforms/FRDM-K64F/) to find which one it is).

1. Follow the [instructions on the mbed handbook](https://developer.mbed.org/handbook/SerialPC) to setup the serial connection with your device from your development machine. If you are on Windows, install the Windows serial port drivers located [here](http://developer.mbed.org/handbook/Windows-serial-configuration#1-download-the-mbed-windows-serial-port).

## Website setup
Follow the instructions in WebSite\WebsitePublish.md to set up the web site code to work with this configuration.

## Create mbed project and import the sample code

1. In your web browser, go to the mbed.org [developer site](https://developer.mbed.org/). If you haven't signed up, you will see an option to create a new account (it's free). Otherwise, log in with your account credentials. Then click on **Compiler** in the upper right-hand corner of the page. This should bring you to the Workspace Management interface.

1. Make sure the hardware platform you're using appears in the upper right-hand corner of the window, or click the icon in the right-hand corner to select your hardware platform.

1. Click **Import** on the main menu. Then click the **Click here** to import from URL link next to the mbed globe logo.

1. In the popup window, enter the link for the sample code https://developer.mbed.org/teams/robot-arm-demo-team/code/RobotArmDemo/ . Note: In the popup window do not click the option to update the libraries. The Azure IoT SDK may be updated in ways that are not compatible with the sample.

1. You can see in the mbed compiler that importing this project imported various libraries. Some are provided and maintained by the Azure IoT team ([azureiot_common](https://developer.mbed.org/users/AzureIoTClient/code/azureiot_common/), [iothub_client](https://developer.mbed.org/users/AzureIoTClient/code/iothub_client/), [iothub_http_transport](https://developer.mbed.org/users/AzureIoTClient/code/iothub_http_transport/), [proton-c-mbed](https://developer.mbed.org/users/AzureIoTClient/code/proton-c-mbed/)), while others are third party libraries available in the mbed libraries catalog.

1. In the IothubRobotArm.cpp file, find and replace values in the following lines of code with your device **connection string** (to obtain this device connection string you can use the node.js tool as described earlier in this tutorial or using device explorer as instructed [here][device-explorer]):
  ` static const char* connectionString = "[connection string]"; `

1. Click **Compile** to build the program. You can safely ignore any warnings, but if the build generates errors, fix them before proceeding.

1. If the build is successful, a .bin file with the name of your project is generated. Copy the .bin file to the device. Saving the .bin file to the device causes the current terminal session to the device to reset. When it reconnects, reset the terminal again manually, or start a new terminal. This enables the mbed device to reset and start executing the program.

1. Connect to the device using an serial terminal client application, such as PuTTY. You can determine which serial port your device uses by checking the Windows Device Manager:

1. In PuTTY, click the **Serial** connection type. The device most likely connects at 115200, so enter that value in the **Speed** box. Then click **Open**: 

### Customizing the device software
1. The file *RobotArmCfg.h* contains an array of joints and their Ids. Each joint is referenced by its index in this array. If you want to have more joints, add an array entry, and change the value of *NUMJOINTS*.

2. The file *RobotNode/RobotNode.h* specifies the types of actuators supported, and the *RobotNode* base class for any actuator. The sample only uses the *AX12* actuator, but an emulated actuator is also included. If you need to add a different type of actuator, create a new class derived from RobotNode, and a value to the *NodePartType* enumeration. The *RobotArm* constructor needs to be updated to recognize a new actuator type.

3. The file *RobotArm.cpp* has the code to initialize the *DynamixelBus*. The sample is using a baud rate of 500,000, as a higher baud rate was found to be unreliable. The AX12 actuators need to be programmed to operate at this baud rate as this is not the default.

4. The programmed actions are defined in *Sequences.cpp*. A sequence can include a series of motions, delays, and iterated loops. Sequences can be modified, and additional sequences can be added.

5. The file *ArmController.cpp* has the code that maps a command coming from IoT hub to a sequence or a state change. The code that runs the state machine and carries out the commands is also here. The frequency of sending measurements to IoT hub can be changed by modifying *SEND_STATUS_TO*.

6. The file *IothubRobotArm.cpp* sends measurements and alerts to IoT hub, and receives commands from IoT hub. You can change the number of sent messages that are not acknowledged. However note that this increases the heap memory usage. If you exceed the available memory, the software may crash and just stop working.

### App is now running on the device!
The program starts executing. You may have to reset the board (press CTRL+Break or press on the board's reset button) if the program does not start automatically when you connect.

After reset the device moves the arm to a set position, and is ready to receive commands from IoT hub. The device is sending telemetry data to IoT hub. If the device detects any problems, such as load too high, it beeps, changes the LED color to red, stops the arm motion, and sends an alert to IoT hub.

The website code receives the data metrics coming from the device via IoT hub, and displays the values as graphs. The website also receives the alerts from the device and displays these in the alerts table.

The website code also sends temperature data to the Azure Anomaly detection job. If the job indicates an anomaly, the website code creates an alert, displays it in the alerts table, and sends an alert message to the device. When the device receives an alert it beeps, changes the LED color to red, and stops the arm motion.

The web page provides a set of clickable links for sending commands to the device via IoT hub. The device will receive these commands and act on them. There are some built in sequences that you can choose from. If the device stopped due to an alert, either self generated or received from IoT hub, the resume command will clear the error state and continue the arm motion.

## More information
To learn more about Azure IoT Hub check out the [Azure IoT Dev center].
In the IoT dev center  you will also find plenty simple samples for connecting many sorts of devices to Azure IoT Hub.

[Azure Management Portal]: https://manage.windowsazure.com
[Azure Preview Portal]: https://portal.azure.com/
[Azure IoT Dev center]: https://www.azure.com/iotdev
[device-explorer]: http://aka.ms/iot-hub-how-to-use-device-explorer
[PuTTY]: http://www.putty.org/
