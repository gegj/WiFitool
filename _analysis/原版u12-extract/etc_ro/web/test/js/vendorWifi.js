/*
 * vendorWifi.js: Dummy implementation of VendorWifi API
 * (c) Penrillian Ltd 2012
 *
 * */

/* There are several special things for testing:
*
* In the password field for Login: 'admin' is the correct initial password (can be changed by ChangePassword).
* 'duplicate' causes the duplicateUser error.
* 'changed' logs in succesfully, then generates the changedLoginStatus automatic log out event.
* 'cellularNetworkError', 'deviceError', 'wifiConnectionError' log in successfully, then generate a
*     receivedNonSpecificError event with that error type.
* Anything else gives the badPassword error.
*
* For convenience, Login accepts just the first 4 chars (any case) of the special items (e.g. 'DUPL').
*
* In the Security Settings page, valid encryption keys (for testing radio buttons changing validation) are:
* For WEP: 10 digit number, or:
*   the current encryption type string (e.g. WPA-PSK).
**/

function say(what) {
    document.writeln(what);
}

function vendorWifi() {
//    Some dummy values for the test implementation.
    var deviceErrorObject = { errorType: 'deviceError', errorId: '123', errorText: 'Some Text' };
    var wifiCallbackDestination = window;
    var currentLoginStatus = 'loggedOut';

    var CurrentPassword = 'admin';
    var DelayOnEachCallMillis = Util.inUnitTest() ? 0 : 250;
    var doSdMemoryError = false;  // True if GetSdMemorySizes is to generate the noSdCardPresent error.
    var fileExistsOnSdCard = false; // True simulates the scenario where a file already exists in folder, used in CheckFileExist

    setTimeout(function (){doSdMemoryError=true; 
        WiFiShared.updateSdMemorySizes($.noop);
    },3000000);  // Set this to a lower value in order to test SD card failure (after said amount of time)

    function sleep(milliseconds) {
        var start = new Date().getTime();
        var i = 0;
        for (; i < 1e7; i++) {
            if ((new Date().getTime() - start) > milliseconds) {
                break;
            }
        }
    }

// Helper function for the test implementation.
// Implements the sync/async protocol (i.e. that all methods may be blocking or with callbacks),
//    and error handling behaviour and normal results.
//
// args: the arguments list passed in to the public method.
// result: the result to return or pass back normally
//
// If result has attribute errorType, it is taken to signal an error, is extended to a full error object
//    and passed back through the error callback.
//

    function doStuff(args, result) {
        var params = args[0], callback = args[1], errorCallback = args[2];
        var objectToReturn;

        if (result && typeof result.errorType === 'string') {
            objectToReturn = $.extend({ errorId: '123', errorText: 'Some Text' }, result);
        }
        else {
            objectToReturn = $.extend({}, result); // Duplicate it.
        }

        if (!callback) {
            sleep(DelayOnEachCallMillis);
            return objectToReturn;
        }

        setTimeout(function () {
            if (Util.isErrorObject(objectToReturn)) {
                errorCallback(objectToReturn);
            } else {
                callback(objectToReturn);
            }
        }, DelayOnEachCallMillis);
    }

    function SetWifiCallbackDestination(destination) {
        wifiCallbackDestination = destination;
    }


    function Login(params, callback, errorCallback) {

        if (typeof params !== 'object' || typeof params.password !== 'string') {
            wifiCallbackDestination.parameterError();
            return;
        }

//        For testing, want to simply enter the first few chars - so use abbreviations:
        function abbreviate(str) {
            return str.slice(0, 4).toLowerCase();
        }
        var fullVersions =
            ['change','cellularNetworkError','deviceError','wifiConnectionError','noSdCardPresent','duplicateUser'];
        var userIntention = $.grep(fullVersions, function (elem) {
            return abbreviate(elem) === abbreviate(params.password);
        });
        var enteredPassword = userIntention.length !== 0 ? userIntention[0] : params.password;

        if (enteredPassword !== CurrentPassword) {
            switch (enteredPassword) {
                case 'change':
                    setTimeout(function () {
                        wifiCallbackDestination.changedLoginStatus({loginStatus:'loggedOut'});
                    }, DelayOnEachCallMillis);
                    break;
                case 'cellularNetworkError':
                case 'deviceError':
                case 'wifiConnectionError':
                    setTimeout(function () {
                        wifiCallbackDestination.receivedNonSpecificError(
                            { errorType: enteredPassword, errorId: '123', errorText: 'Some Text' }
                        );
                    }, DelayOnEachCallMillis);
                    break;

                case 'noSdCardPresent':
                    doSdMemoryError = true;
                    break;

                default:  // Immediate error
                    return doStuff(arguments, {
                        errorType: (enteredPassword === 'duplicateUser') ? 'duplicateUser' : 'badPassword'
                    });
            }
        }

        // Either correct password, or one of the 'magic' passwords above:
        currentLoginStatus = 'loggedIn';
        return doStuff(arguments, {});
    }

    function Logout(/*params, callback, errorCallback*/) {
        currentLoginStatus = 'loggedOut'; // Not doing a simulation of failure
        setTimeout(loggedOut, 2000);
        return doStuff(arguments, {});
    }

    function ChangePassword(params)
    {
        if (params.oldPassword !== CurrentPassword) {
            return doStuff( arguments, {errorType: 'badPassword' } );
        }
        CurrentPassword = params.newPassword;
        return doStuff(arguments, {});
    }

    function LoginStatus() {
        return doStuff(arguments, {status: currentLoginStatus});
    }

    var vendorFileUploadConfig = {
        vendorUploadAction: '/api/sdcard/fileupload',
        hiddenInputHtml:    '<input type="hidden" name="randomNum" id="randomNum" value="' + Math.round(Math.random() * 10000) + '"/>'
    };

    var vendorRestoreFileUploadConfig = {
        vendorUploadAction: '/api/nvramul.cgi',
        hiddenInputHtml:    '<input type="hidden" name="randomNum" id="randomNum" value="' + Math.round(Math.random() * 10000) + '"/>'
    };

    var validUrlForFileDownload =
        'http://sourceforge.net/projects/symbianosunit/files/symbianosunit/Release%20V1.04/SymbianOsUnit1_04.zip/download';
    // Note: Device returns size of zero for folders (at present).
    var rootDirectoryList = [
                { name: 'andover1.txt', lastUpdated: '2012-01-31 17:57:08', size: '5000', type: 'file', fileUrlOrFolderPath: validUrlForFileDownload },
                { name: 'humeli', lastUpdated: '2012-01-12 16:54:02', size: '0', type: 'folder', fileUrlOrFolderPath: 'folder444' },
                { name: 'andover2.txt', lastUpdated: '2012-01-06 13:17:28', size: '5000', type: 'file', fileUrlOrFolderPath: validUrlForFileDownload },
                { name: 'jumeli', lastUpdated: '2012-02-01 17:57:08', size: '0', type: 'folder', fileUrlOrFolderPath: 'folder555' },
                { name: 'andover3.txt', lastUpdated: '2012-01-13 12:57:08', size: '5000', type: 'file', fileUrlOrFolderPath: validUrlForFileDownload },
                { name: 'Zfile', lastUpdated: '2012-01-31 17:57:08', size: '2000000', type: 'file', fileUrlOrFolderPath: validUrlForFileDownload },
                { name: 'My Documents', lastUpdated: '2012-01-31 17:57:08', size: '0', type: 'folder', fileUrlOrFolderPath: 'folder666' },
                { name: 'Videos', lastUpdated: '2012-01-31 17:57:08', size: '0', type: 'folder', fileUrlOrFolderPath: 'folder777'},
                { name: 'Pictures', lastUpdated: '2012-01-31 17:57:08', size: '0', type: 'folder', fileUrlOrFolderPath: 'folder888'},
                { name: 'my stuff', lastUpdated: '2012-01-31 17:57:08', size: '0', type: 'folder', fileUrlOrFolderPath: 'folder999'}
                ];

    var subDirectoryList = [
                { name: 'abc', lastUpdated: '2012-01-31 17:57:08', size: '0', type: 'folder', fileUrlOrFolderPath: 'folderzzz' },
                { name: 'defg', lastUpdated: '2012-01-31 17:57:08', size: '0', type: 'folder', fileUrlOrFolderPath: 'folderaaa' },
                { name: 'hij', lastUpdated: '2012-01-31 17:57:08', size: '0', type: 'folder', fileUrlOrFolderPath: 'folderbbb' }
                ];

    function GetFileList( params ) {
        var resultToReturn = (params.folderPath === '/' || params.folderPath === '') ?
        {
            files: rootDirectoryList
        } :
        {
            files: subDirectoryList
        };

        return doStuff( arguments, resultToReturn );
    }

   function CreateFolder(params) {
       // Support only creating in root folder.
       rootDirectoryList.push({ name: params.folderName, lastUpdated: '2012-02-02 18:19:00', size: '', type: 'folder', fileUrlOrFolderPath: '/' + params.folderName });
       return doStuff(arguments, { errorType: 'noSdCardPresent'});
   }

   function DeleteFilesAndFolders(params) {
       var namesToDelete = Util.collect(params.files, function (thing) {
           return thing.name;
       });
       rootDirectoryList = $.grep( rootDirectoryList, function(element, index) {
           return $.inArray(element.name, namesToDelete) === -1;
        });

       return doStuff(arguments, {});
   }

  function CheckFileExists(params){
      var fileName = params.sourcePathOnLocalMachine.split('\\').pop();
      rootDirectoryList.push({ name: fileName, lastUpdated: '2012-02-02 18:19:00', size: '10245', type: 'file', fileUrlOrFolderPath: validUrlForFileDownload });

      var fileExistsResult = fileExistsOnSdCard ? { errorType: 'fileExists' } : {};
      return doStuff(arguments, doSdMemoryError ?
      {
          errorType: 'noSdCardPresent'
      } : fileExistsResult);
  }

    function CheckUploadFileStatus(params) {
        if (params.isErrorTest) {
            doSdMemoryError = true; // force test case
        }
        return doStuff(arguments, doSdMemoryError ?
        {
            errorType: 'noSdCardPresent'
        } : {});
    }

// *********************************************************************************************************
//  Periodic update callbacks:
// *********************************************************************************************************
    var updateNumber = 0;

    function doPeriodicUpdates() {
        updateNumber++;
        doOnePeriodicUpdate(updateNumber);
    }

    function doOnePeriodicUpdate(updateNum) {
        if (typeof wifiCallbackDestination.setBatteryStatus !== 'function') {
            return;
        }

        // A number that increments every n calls, up to m, then restarts at 0.
        function changeEvery(n, m) {
            return (Math.floor(updateNum/ n)) % (m + 1);
        }

        wifiCallbackDestination.setBatteryStatus({
            batteryStatus: changeEvery(30, 1) ? 'charging' : 'use',
            batteryLevel: 75,
            // Re-op this to see battery go up to 100, then 0 and get the pop up warning.
            //batteryLevel: changeEvery(2, 100).toString(),
            batteryTime: changeEvery(1, 1000).toString()
        });

        var enabled = (changeEvery(10, 4) !== 0);
        wifiCallbackDestination.setWifiStatus({
            wifiStatus: enabled ? 'enabled' : 'disabled',
            ssid: enabled ? 'MOBOS_WIRELESS' : '',
            security: ['', 'NONE', 'WEP', 'AES', 'TKIP'][changeEvery(10, 4)]
        });

        var devices = [
            { deviceName: 'fred' },
            { deviceName: 'jim' },
            { deviceName: 'sheila' },
            { deviceName: 'robert' }
        ];
        devices.splice(changeEvery(10, 4)); // Knocks out a varying number of devices
        wifiCallbackDestination.setConnectedDevices({devices: devices });
    }

    setInterval(doPeriodicUpdates, 1000);

    function ForceCallbacks() {
        doOnePeriodicUpdate();
    }

// ******************************************************************************************************
// Support for Parameter Validation
//    Note: these are not as rigorous as they need to be.  Especially validateEncryptionKey
// ******************************************************************************************************

    function GetParameterValidation() {
        return {
            maxPassword: 20,
            maxSsid: 32,
            maxWifiChannel: 14,
            maxPortMappingApplicationName: 30,
            maxMacSettingsDescription: 50,
            validatePassword: function ( password ) { return (/[^%{}]+/).test(password); },
            validateSsid: function ( ssid ) { return (/[0-9A-Za-z-_.]+/).test(ssid); },
            validateFolderName: function ( folderName ) { return (/[^\/]+/).test(folderName); },
            validateEncryptionKey: function (key, authenticationMode) {
                return (/[0-9A-Fa-f]{10}/).test(key) || key === authenticationMode; },  // Testing: key can be encry type.
            validateIpAddress: function (ipAddress) { return (/\b(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\b$/).test(ipAddress); },
            // ref. http://stackoverflow.com/questions/5360768/regular-expression-for-subnet-masking
            validateSubnetMask: function (subnetMask) { return (/^(((255\.){3}(255|254|252|248|240|224|192|128|0+))|((255\.){2}(255|254|252|248|240|224|192|128|0+)\.0)|((255\.)(255|254|252|248|240|224|192|128|0+)(\.0+){2})|((255|254|252|248|240|224|192|128|0+)(\.0+){3}))$/).test(subnetMask); },
            validatePort: function (port) {return ( (/^\d+$/).test(port) && parseInt(port,10)<65536 && 0<parseInt(port,10) );},
            validateMacAddress: function (macAddress) {
                return (/^([0-9a-fA-F][0-9a-fA-F]:){5}([0-9a-fA-F][0-9a-fA-F])$/).test(macAddress); }

        };
    }

// ******************************************************************************************************
// Support for Get/Set combinations
// ******************************************************************************************************

    // Answers a function that answers aSettingsObject
    function makeGetterFunction( aSettingsObject ) {
        return function () {
            return doStuff(arguments, aSettingsObject);
        };
    }

    // Answers a function that sets aSettingsObject from the parameter passed in.
    function makeSetterFunction( aSettingsObject ) {
        return function () {
            $.extend( aSettingsObject, arguments[0] );     // Copy all the parameters over.
            return doStuff( arguments, {} );
        };
    }

    var idleTime = {value: '600'} ;

    var GuestUserControl = {
        allowedToConnect: true,
        allowedToAccessSettings: false
    };

    var WifiSettings = {
       wifiEnabled: false,
        broadcastSsidEnabled: true,
        selectedChannel: '0',
        supportedModes: ['b', 'g', 'b/g', 'b/g/n'],
        wifiMode: 'b/g',
        ssid:  'VodafoneMobileWiFi-123456'
    };

    var MacSettings = {
        macFilteringEnabled: false,
        macFilteringMode: 'whitelist',
        authorizedDevices: [
            { macAddress: '01:01:01:01:01:01', name: "Fred", description: "Fred's PC", connectionType: 'disallowed' },
            { macAddress: '02:01:01:01:01:01', name: "Jim", description: "Jim's PC", connectionType: 'allowed' },
            { macAddress: '03:01:01:01:01:01', name: "New", description: "Sheila's PC", connectionType: 'allowed' }
            ]
        };

    var LIST_WIFIAUTHENTICATIONMODES = [];
    LIST_WIFIAUTHENTICATIONMODES[0] = {authenticationMode: 'NONE', validEncryptionTypes: []};
    LIST_WIFIAUTHENTICATIONMODES[1] = {authenticationMode: 'OPEN', validEncryptionTypes: ['WEP']};
    LIST_WIFIAUTHENTICATIONMODES[2] = {authenticationMode: 'SHARE', validEncryptionTypes: ['WEP']};
    LIST_WIFIAUTHENTICATIONMODES[3] = {authenticationMode: 'AUTO', validEncryptionTypes: ['WEP']};
    LIST_WIFIAUTHENTICATIONMODES[4] = {authenticationMode: 'WPA-PSK', validEncryptionTypes: ['AES', 'TKIP', 'MIX' ]};
    LIST_WIFIAUTHENTICATIONMODES[5] = {authenticationMode: 'WPA2-PSK', validEncryptionTypes: ['AES', 'TKIP', 'MIX' ]};
    LIST_WIFIAUTHENTICATIONMODES[6] = {authenticationMode: 'WPA/WPA2-PSK', validEncryptionTypes: ['AES', 'TKIP', 'MIX' ]};

    var WifiSecuritySettings = {
        encryptionKey: '',
        authenticationMode:'WPA2-PSK',
        validAuthenticationModes: LIST_WIFIAUTHENTICATIONMODES,
        encryptionType: 'MIX'
    };

    function GetWifiSecuritySettings() { // Handle special - encryption key is always null string on Get.
        return doStuff( arguments, $.extend( {}, WifiSecuritySettings, {encryptionKey: ''} ) );
    }

    var  MobileWifiDiagnostics = {
        productName: 'Vodafone R205',
        softwareVersion: '1.0 software',
        modemVersion: '1.0 modem',
        routerVersion: '1.0 router',
        hardwareVersion: '1.0 hardware',
        serialNumber: '12345serialNumber',
        simSerialNumber: '12345678901234',
        simMsisdn: '12345678901234567890',
        deviceImei: '12345678901234567890',
        simImsi: '098765432109876543231',
        simStatus: "index",
        sdCardAvailable: true,
        sdCardTotalMemory: '1000000000',
        sdCardAvailableMemory: '500000000',
        currentConnectedUsers: '3',
        maxConnectedUsers: '5',
        timeSinceStartup: updateNumber.toString()
    };

    var  RouterDiagnostics = {
        dhcpEnabled: true,
        dmzEnabled: false
    };

    var CurrentlyAttachedDevices = {

        attachedDevices: [
            { ipAddress: '127.78.01.05', hostName: 'fred', macAddress: "02:01:01:01:01:01", timeConnected: '6789' },
            { ipAddress: '127.78.01.08', hostName: 'john', macAddress: "02:01:01:0A:01:01", timeConnected: '9789' },
            { ipAddress: '127.74.01.01', hostName: 'fred', macAddress: "02:01:01:01:0F:01", timeConnected: '689' }
            ]
    };

   function GetSdMemorySizes() {
       return doStuff(arguments, doSdMemoryError ?
       {
           errorType: 'noSdCardPresent'
       } : {
           totalMemorySize: "20000000000",
           availableMemorySize: "500000000"
       });
   }

    var SdCardSharing = {
        access: 'readOnly',
        sharedFolder: 'folder555',
        sdCardStatus: true
    };

    var RouterIpConfiguration={
      ipAddress:"192.168.0.2",
      subnetMask:"255.255.128.0",
      lanDomain: 'VodafoneMobile.wifi',
      switchBatteryOffWhenIdle: true
    };
    var RouterDhcpConfiguration = {
        dhcpEnabled: false,
        dhcpRangeStart:"192.168.0.2",
        dhcpRangeEnd: '192.168.0.100',
        dhcpLeaseTime: '604800'
    };
    var natSettings={
        natEnabled:false,
        // Note - this not supported by R205 so not used at present
        natType:"symmetric"
    };
    var applicationPortMappings={
         enabled:false,
         portMappings: [
            { applicationName: 'app1', applicationProtocol: 'udp', sourcePort: "80", destinationIp: '192.127.127.0',destinationPort:'13'},
            { applicationName: 'app2', applicationProtocol: 'tcp', sourcePort: "8080", destinationIp: '192.127.127.8',destinationPort:'56'},
            { applicationName: 'app3', applicationProtocol: 'tcp', sourcePort: "978", destinationIp: '192.127.127.4',destinationPort:'678'}
            ]
    };
    var dmzSettings={
        dmzEnabled: true,
        dmzIpAddress: "192.168.34.74"
    };
    var routerBackupUrl={
        backupUrl:"version.xml"
    };

    var webUiData = {
    };

    var vodafoneConfiguration = {
        //Enterprise None P&P
        //Vodafone Mobile Wi-Fi
        //Vodafone Pocket WiFi

        sku: 'Vodafone Mobile Wi-Fi',
        maxConnectedDevices: '5'
    };

    var SDCardName = {sdCardName : 'MicroSD Card'};

    return {
        Login: Login,
        LoginStatus: LoginStatus,
        Logout: Logout,
        ChangePassword:ChangePassword,
        GetGuestUserControl: makeGetterFunction(GuestUserControl),
        SetGuestUserControl: makeSetterFunction(GuestUserControl),
        GetWifiSettings: makeGetterFunction(WifiSettings),
        SetWifiSettings: makeSetterFunction(WifiSettings),
        GetMacSettings: makeGetterFunction(MacSettings),
        SetMacSettings: makeSetterFunction(MacSettings),
        GetWifiSecuritySettings: GetWifiSecuritySettings,
        SetWifiSecuritySettings: makeSetterFunction(WifiSecuritySettings),
        GetMobileWifiDiagnostics: makeGetterFunction(MobileWifiDiagnostics),
        GetRouterDiagnostics: makeGetterFunction(RouterDiagnostics),
        GetCurrentlyAttachedDevices: makeGetterFunction(CurrentlyAttachedDevices),
        GetVendorFileUploadConfig: makeGetterFunction(vendorFileUploadConfig),
        GetVendorRestoreFileUploadConfig: makeGetterFunction(vendorRestoreFileUploadConfig),
        GetSdMemorySizes: GetSdMemorySizes,
        GetFileList: GetFileList,
        DeleteFilesAndFolders: DeleteFilesAndFolders,
        CreateFolder: CreateFolder,
        CheckFileExists: CheckFileExists,
        CheckUploadFileStatus: CheckUploadFileStatus,
        GetSdCardSharing: makeGetterFunction(SdCardSharing),
        SetSdCardSharing: makeSetterFunction(SdCardSharing),
        GetRouterIpConfiguration: makeGetterFunction(RouterIpConfiguration),
        SetRouterIpConfiguration: makeSetterFunction(RouterIpConfiguration),
        GetRouterDhcpConfiguration: makeGetterFunction(RouterDhcpConfiguration),
        SetRouterDhcpConfiguration: makeSetterFunction(RouterDhcpConfiguration),
        GetNatSettings: makeGetterFunction(natSettings),
        SetNatSettings: makeSetterFunction(natSettings),
        GetApplicationPortMappings:makeGetterFunction(applicationPortMappings),
        SetApplicationPortMappings:makeSetterFunction(applicationPortMappings),
        GetDmzSettings:makeGetterFunction(dmzSettings),
        SetDmzSettings:makeSetterFunction(dmzSettings),
        GetRouterBackupUrl:makeGetterFunction(routerBackupUrl),
        GetWebUiData:makeGetterFunction(webUiData),
        SetWebUiData:makeSetterFunction(webUiData),
        GetVodafoneConfiguration: makeGetterFunction(vodafoneConfiguration),
        GetIdleTime: makeGetterFunction(idleTime),
        SetIdleTime: makeSetterFunction(idleTime),
        SetWifiCallbackDestination: SetWifiCallbackDestination,
        GetParameterValidation: GetParameterValidation,
        GetSdCardName: function(){return SDCardName;},

        ForceCallbacks: ForceCallbacks,

        testDemo: { doOnePeriodicUpdate: doOnePeriodicUpdate }
    };
}

var VendorWifi = vendorWifi();

 //say('vendorWifi.js loaded');
