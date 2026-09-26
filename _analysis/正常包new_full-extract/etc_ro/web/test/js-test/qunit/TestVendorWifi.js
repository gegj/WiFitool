// Tests for Vendor Wifi API
// (c) Penrillian Ltd 2012
//

var VendorWifiTests = (function VendorWifiTests() {

    // Uncomment this for the ultimate acid test of CallbackWrapper!!  It works 29/9/11.
    //var VendorWifi = CallbackWrapper.wrapObject( window.VendorWifi );

var CallbackResults = null;
var CallbackDestination = { };
var TimeBeforeAllCallbacksHaveHappenedMillis = 2000;

module('VendorWifi', {
    setup: function () {
        VendorWifi.SetWifiCallbackDestination(CallbackDestination); // Actually only need to do it once, but won't do any harm.
        CallbackResults = {};
    },
    teardown: function () {
        CallbackDestination.parameterError = CallbackDestination.changedLoginStatus = CallbackDestination.receivedNonSpecificError
            = CallbackDestination.setBatteryStatus = CallbackDestination.setConnectedDevices = CallbackDestination.setWifiStatus
            = $.noop;
        VendorWifi = vendorWifi();
    }
});

// ***************************************************************************************************
// The main tests - for both live and demo system
// ***************************************************************************************************

test('Synchronous Login fail', function() {
    var result = VendorWifi.Login({ password: 'NotAdmin' });
    ok(Util.isErrorObject(result), 'Result is error');
    equals( result.errorType, 'badPassword');
});


test('Synchronous Login success', function() {
    var result = VendorWifi.Login({ password: 'admin' });
    ok(result);
    ok(!Util.isErrorObject(result), 'Not error');
});

test('Synchronous logout', function() {
    var result = VendorWifi.Logout(null);
    ok(result);
});

test("Sequence: Login, Logout and LoginStatus", function() {
//  Could code this async, but requires too many nested functions.
//  We'll take the hit on performance.

    var loggedIn = VendorWifi.LoginStatus();
    equals(loggedIn.status, 'loggedOut');
    var loginResult = VendorWifi.Login({password: 'admin'});
    equals(typeof loginResult, 'object');
    loggedIn = VendorWifi.LoginStatus();
    equals(loggedIn.status, 'loggedIn');
    var logoutResult = VendorWifi.Logout();
    equals(typeof logoutResult, 'object');
    loggedIn = VendorWifi.LoginStatus();
    equals(loggedIn.status, 'loggedOut');
});


test("Get/Set GuestUserControl", function () {
    var userControl = VendorWifi.GetGuestUserControl();
    validateObject(userControl, {
        allowedToConnect: isBoolean,
        allowedToAccessSettings: isBoolean
    }, 'initial GetGuestUserControl');

    var result = VendorWifi.SetGuestUserControl({
        allowedToConnect: true,
        allowedToAccessSettings: false
    });
    ok(result, 'SetGuestUserControl');

    userControl = VendorWifi.GetGuestUserControl();
    validateObject(userControl, {
        allowedToConnect: true,
        allowedToAccessSettings: false
    }, 'GetGuestUserControl after setting');
});

test("GetSdCardName", function () {
    var sdCardName = VendorWifi.GetSdCardName();
    validateObject(sdCardName, {
        sdCardName: isString
    }, 'GetSdCardName');
    equals(sdCardName.sdCardName, 'MicroSD Card', 'GetSdCardName');
});

test("Get/Set WifiSettings", function () {
    var settings = VendorWifi.GetWifiSettings();
    validateObject(settings, {
        wifiEnabled: isBoolean,
        broadcastSsidEnabled: isBoolean,
        selectedChannel: makeEnumerationChecker( '0', '1', '2', '3', '4', '5','6', '7', '8', '9', '10', '11', '12', '13', '14' ),
        supportedModes: isArrayOfStrings,
        wifiMode: makeEnumerationChecker( settings.supportedModes ),
        ssid:  isString
    }, 'initial GetWifiSettings');

    var settingsToSet = {
       wifiEnabled: true,
        broadcastSsidEnabled: true,
        selectedChannel: '2',
        wifiMode: 'b/g',
        ssid:  'Testing SSID'
    };

    var result = VendorWifi.SetWifiSettings( settingsToSet );
    ok(result, 'SetWifiSettings');

    settings = VendorWifi.GetWifiSettings();
    $.extend( settingsToSet, {supportedModes: isArrayOfStrings} );
    validateObject(settings, settingsToSet );  // All should now be as we set them.
});

test("Get/Set MacSettings", function () {
    var settings = VendorWifi.GetMacSettings();

    validateObject(settings, {
        macFilteringEnabled: isBoolean,
        macFilteringMode: makeEnumerationChecker('whitelist', 'blacklist'),
        authorizedDevices: makeObjectArrayValidator({
            macAddress: isMacAddressString,
            name: isString,
            description: isString,
            connectionType: makeEnumerationChecker('allowed', 'disallowed')
        }, 'authorizedDevices')
    }, 'initial GetMacSettings');

    var settingsToSet = {
        macFilteringEnabled: true,
        macFilteringMode: 'blacklist',
        authorizedDevices: [
            { macAddress: '01:01:01:01:01:01', name: "Fred", description: "Fred's PC", connectionType: 'allowed' },
            { macAddress: '02:01:01:01:01:01', name: "Jim", description: "Jim's PC", connectionType: 'allowed'},
            { macAddress: '04:01:01:01:01:01', name: "New", description: "New PC", connectionType: 'disallowed'}
            ]
        };

    var result = VendorWifi.SetMacSettings( settingsToSet );
    ok(result, 'SetMacSettings');

    settings = VendorWifi.GetMacSettings();
    validateObject(settings, settingsToSet );  // All should now be as we set them.
});


    test("Get/Set WifiSecuritySettings - object work", function () {
        var settings = VendorWifi.GetWifiSecuritySettings();

        validateObject(settings, {
            encryptionKey: '',
            authenticationMode: makeEnumerationChecker(Util.collect(settings.validAuthenticationModes, function (item) {return item.authenticationMode;})),
            validAuthenticationModes: makeObjectArrayValidator({authenticationMode:isFullString, validEncryptionTypes:makeValidEncryptionTypesChecker()}, 'list of wifi authentication modes'),
            encryptionType: isFullString
        }, 'initial GetWifiSecuritySettings');

        var settingsToSet = {
            authenticationMode: 'AUTO',
            encryptionKey: '0123456789',
            encryptionType: 'WEP'
        };

        var result = VendorWifi.SetWifiSecuritySettings(settingsToSet);
        ok(result, 'SetWifiSecuritySettings');

        settings = VendorWifi.GetWifiSecuritySettings();
        validateObject(settings, {
            encryptionKey: '',
            authenticationMode: 'AUTO',
            validAuthenticationModes: makeObjectArrayValidator({authenticationMode:isFullString, validEncryptionTypes:makeValidEncryptionTypesChecker()}, 'list of wifi authentication modes'),
            encryptionType: 'WEP'
        }, 'initial GetWifiSecuritySettings');
    });

    test("Get/Set WifiSecuritySettings - integrity of object", function () {
           var settings = VendorWifi.GetWifiSecuritySettings();
           var authenticationModes = Util.collect(settings.validAuthenticationModes, function (item) {
               return item.authenticationMode;
           });
           var authenticationMode = $.grep(settings.validAuthenticationModes, function (item) {
               return item.authenticationMode === settings.authenticationMode;
           });
           authenticationMode = $(authenticationMode).get(0);
           if (authenticationMode === undefined) {
               authenticationMode = [];
           }
           validateObject(settings, {
               encryptionKey: '',
               validAuthenticationModes: isArrayofObjects,
               authenticationMode: makeEnumerationChecker(authenticationModes),
               encryptionType: makeEnumerationChecker(authenticationMode.validEncryptionTypes)
           }, 'initial GetWifiSecuritySettings');

           var settingsToSet = {
               encryptionKey: '0123456789',
               authenticationMode: 'Auto',
               encryptionType: 'WEP'
           };

           var result = VendorWifi.SetWifiSecuritySettings(settingsToSet);
           ok(result, 'SetWifiSecuritySettings');

           settings = VendorWifi.GetWifiSecuritySettings();
           validateObject(settings, {
               encryptionKey: '',
               validAuthenticationModes: isArrayofObjects,
               authenticationMode: 'Auto',
               encryptionType: 'WEP'
           }, 'initial GetWifiSecuritySettings');
       });




test("GetMobileWifiDiagnostics", function () {
    var settings = VendorWifi.GetMobileWifiDiagnostics();
    validateObject(settings, {
        productName: isFullString,
        softwareVersion: isFullString,
        modemVersion: isFullString,
        routerVersion: isFullString,
        hardwareVersion: isFullString,
        serialNumber: isFullString,
        simSerialNumber: isFullString,
        simMsisdn: isFullString,
        deviceImei: isFullString,
        simImsi: isFullString,
        simStatus: makeEnumerationChecker( "index", "sim-error", "pin-required", "puk-lock", "puk-required" ),
        sdCardAvailable: isBoolean,
        sdCardTotalMemory: isIntegerString,
        sdCardAvailableMemory: isIntegerString,
        currentConnectedUsers: isIntegerString,
        maxConnectedUsers: isIntegerString,
        timeSinceStartup: isIntegerString
    }, 'GetMobileWifiDiagnostics');
    equals(settings.maxConnectedUsers, '5', 'Expect maxConnectedUsers to be 5');
});

test("GetRouterDiagnostics", function () {
    var settings = VendorWifi.GetRouterDiagnostics();
    validateObject(settings, {
        dhcpEnabled: isBoolean,
        dmzEnabled: isBoolean
    }, 'GetRouterDiagnostics');
});

test("GetCurrentlyAttachedDevices", function () {
    var settings = VendorWifi.GetCurrentlyAttachedDevices();
    validateObject(settings, {
        attachedDevices: makeObjectArrayValidator( {
            ipAddress: isIpAddressString,
            hostName: isFullString,
            macAddress: isMacAddressString,
            timeConnected: isIntegerString
        }, 'attachedDevices' )
    }, 'GetCurrentlyAttachedDevices');
});

test("GetSdMemorySizes", function () {
    var settings = VendorWifi.GetSdMemorySizes();
    validateObject(settings, {
        totalMemorySize: isIntegerString,
        availableMemorySize: isIntegerString
    }, 'GetSdMemorySizes');
});


function validateFileList(fileList) {
    ok(fileList);
    validateObject(fileList, {
        files: makeObjectArrayValidator({
            name: isFullString,
            lastUpdated: isFullString,
            size: isIntegerOrNullString,
            type: makeEnumerationChecker('file', 'folder'),
            fileUrlOrFolderPath: isFullString
            //lastUpdated: isFullString // Hope we won't need this.
        }, 'files')
    }, 'GetFileList');
}

test("GetFileList", function () {
    var fileList = VendorWifi.GetFileList( {folderPath: ''} );
    validateFileList(fileList);
});


// Utility function to return an array of 0 or 1 file details in the root folder matching the given name:
function getEntryFor(name, folder) {
    folder = folder || '';
    var fileList = VendorWifi.GetFileList({folderPath: folder});
    validateFileList(fileList);
    return $.grep(fileList.files, function(element, index) {
        return element.name === name;
    });
}

test("Delete, Create, list folder", function () {
    var folderNameToUse = 'fred';
    // Tidy up first - there may be a folder/file of that name already.
    VendorWifi.DeleteFilesAndFolders({ folderPath: '', files: [
        { name: folderNameToUse }
    ] });  // Don't care about result.
    var result = VendorWifi.CreateFolder({folderPath: '', folderName: folderNameToUse});
    ok( result );

    var entries = getEntryFor(folderNameToUse);
    equals( entries.length, 1, 'Found ' + folderNameToUse + ' in root directory');

    fileList = getEntryFor(entries[0].fileUrlOrFolderPath );
    equals(fileList.length, 0, 'Expect no files in new directory');

    result = VendorWifi.DeleteFilesAndFolders({ folderPath: '', files: [
        { name: folderNameToUse }
    ] });
    ok(result);

    entries = getEntryFor(folderNameToUse);
    equals( entries.length, 0, folderNameToUse + ' should be deleted');

});
// TODO: Delete multiple and uploading file to subfolders.

test("Upload and delete file", function () {
    var fileNameToUse = 'Uploaded File.txt'; // This should be the full path
    var filePathToUse = 'c:\\somewhere\\';

    // Tidy up first - there may be a folder/file of that name already.
    VendorWifi.DeleteFilesAndFolders({ folderPath: '', files: [
        { name: fileNameToUse }
    ] });  // Don't care about result.

    var entries = getEntryFor(fileNameToUse);
    equals( entries.length, 0, 'Expected no ' + fileNameToUse + ' in root directory');

    var result = VendorWifi.CheckFileExists({
        destinationFolderOnDevice:  '',
        sourcePathOnLocalMachine: filePathToUse + fileNameToUse
    });
    ok( $.isEmptyObject(result) );

    entries = getEntryFor(fileNameToUse);
    equals( entries.length, 1, 'Expected ' + fileNameToUse + ' in root directory');

    result = VendorWifi.DeleteFilesAndFolders({ folderPath: '', files: [
        { name: fileNameToUse }
    ] });
    ok(result);

    entries = getEntryFor(fileNameToUse);
    equals( entries.length, 0, fileNameToUse + ' should be deleted');

});

    test("Get/Set SdCardSharing", function () {
        var settings = VendorWifi.GetSdCardSharing();
        validateObject(settings, {
            access: makeEnumerationChecker('none', 'readOnly', 'readWrite'),
            sharedFolder: isString,
            sdCardStatus: isBoolean
        }, 'initial GetSdCardSharing');

        var settingsToSet = {
            access: 'readOnly',
            sharedFolder: 'documents',
            sdCardStatus: true
        };

        var result = VendorWifi.SetSdCardSharing( settingsToSet );
        ok(result, 'SetSdCardSharing');

        settings = VendorWifi.GetSdCardSharing();
        validateObject(settings, settingsToSet );  // All should now be as we set them.

    });

    
test("Get/Set RouterIpConfiguration", function () {
    var settings = VendorWifi.GetRouterIpConfiguration();
    validateObject(settings, {
        ipAddress: isIpAddressString,
        subnetMask: isSubnetMaskString,
        lanDomain: isFullString,
        switchBatteryOffWhenIdle: isBoolean
    }, 'initial GetRouterIpConfiguration');

    var settingsToSet = {
        ipAddress: '192.168.0.1',
        subnetMask: '255.255.255.0',
        lanDomain: 'VodafoneMobile.wifi',
        switchBatteryOffWhenIdle: true
    };

    var result = VendorWifi.SetRouterIpConfiguration( settingsToSet );
    ok(result, 'SetRouterIpConfiguration');

    // !!! Hmmm - setting this will probably disconnect the router, so this last will fail in real life!
    settings = VendorWifi.GetRouterIpConfiguration();
    validateObject(settings, settingsToSet );  // All should now be as we set them.
});

test("Get/Set RouterDhcpConfiguration", function () {
    var settings = VendorWifi.GetRouterDhcpConfiguration();
    validateObject(settings, {
        dhcpEnabled: isBoolean,
        dhcpRangeStart: isIpAddressString,
        dhcpRangeEnd: isIpAddressString,
        dhcpLeaseTime: isIntegerString
    }, 'initial GetRouterDhcpConfiguration');

    var settingsToSet = {
        dhcpEnabled: true,
        dhcpRangeStart: '192.168.0.50',
        dhcpRangeEnd: '192.168.0.100',
        dhcpLeaseTime: '86400'
    };

    var result = VendorWifi.SetRouterDhcpConfiguration( settingsToSet );
    ok(result, 'SetRouterDhcpConfiguration');

    settings = VendorWifi.GetRouterDhcpConfiguration();
    validateObject(settings, settingsToSet );  // All should now be as we set them.
});

test('Synchronous Change Password', function() {
    var result = VendorWifi.ChangePassword({ oldPassword: 'admin', newPassword: 'admin2' });
    ok(!Util.isErrorObject(result), 'Password change OK');
    result = VendorWifi.ChangePassword({ oldPassword: 'admin', newPassword: 'admin2' });
    ok(Util.isErrorObject(result), 'Password change fails if old password wrong');
    equals(result.errorType, 'badPassword');
    result = VendorWifi.ChangePassword({ oldPassword: 'admin2', newPassword: 'admin' });
    ok(!Util.isErrorObject(result), 'Password change back OK');
});

test("Get/Set NatSettings", function () {
    var settings = VendorWifi.GetNatSettings();
    validateObject(settings, {
        natEnabled: isBoolean,
        natType: makeEnumerationChecker('symmetric', 'cone')
    }, 'initial GetNatSettings');

    var settingsToSet = {
        natEnabled: true,
        natType: 'cone'
    };

    var result = VendorWifi.SetNatSettings( settingsToSet );
    ok(result, 'SetNatSettings');

    settings = VendorWifi.GetNatSettings();
    validateObject(settings, settingsToSet );  // All should now be as we set them.
});

test("Get/Set ApplicationPortMappings",function() {
    var settings = VendorWifi.GetApplicationPortMappings();
    ok(settings);
    validateObject(settings, {
                enabled: isBoolean,
                portMappings: makeObjectArrayValidator({
                applicationName: isFullString,
                applicationProtocol: makeEnumerationChecker('udp','tcp'),
                sourcePort: isString,
                destinationIp: isIpAddressString,
                destinationPort:isIntegerString
            })
        },'GetApplicationPortMappings');

        var settingsToSet = {
        enabled : true,
        portMappings: [
            { applicationName: 'app1', applicationProtocol: 'udp', sourcePort: "80", destinationIp: '192.127.0.1',destinationPort:'13'},
            { applicationName: 'app2', applicationProtocol: 'tcp', sourcePort: "8080", destinationIp: '192.127.0.3',destinationPort:'56'},
            { applicationName: 'app3', applicationProtocol: 'tcp', sourcePort: "978", destinationIp: '192.127.0.6',destinationPort:'678'}
            ]
        };
        var result = VendorWifi.SetApplicationPortMappings( settingsToSet );
        ok(result, 'SetApplicationPortMappings');

        settings = VendorWifi.GetApplicationPortMappings();
        validateObject(settings, settingsToSet );  // All should now be as we set them.
});

test("Get/Set DmzSettings", function () {
    var settings = VendorWifi.GetDmzSettings();
    validateObject(settings, {
        dmzEnabled: isBoolean,
        dmzIpAddress: isIpAddressString
    }, 'initial GetDmzSettings');

    var settingsToSet = {
        dmzEnabled: true,
        dmzIpAddress: '192.127.0.1'
    };

    var result = VendorWifi.SetDmzSettings( settingsToSet );
    ok(result, 'SetDmzSettings');

    settings = VendorWifi.GetDmzSettings();
    validateObject(settings, settingsToSet );  // All should now be as we set them.
});

test("GetRouterBackupUrl", function () {
    var settings = VendorWifi.GetRouterBackupUrl();
    validateObject(settings, {
        backupUrl: isString
    }, 'GetRouterBackupUrl');
});

test("Get/Set IdleTime", function () {
    VendorWifi.SetIdleTime({value: '300'}) ;

    var idleTime = VendorWifi.GetIdleTime();
    ok(idleTime);
    equals(idleTime.value, '300');
});

test('Get/Set WebUiData', function() {
    
    var settings = VendorWifi.GetWebUiData();

    // Don't know exactly what the parameters will be, but all settings will be strings:
    $.each( settings, function(key, value) {
       equals(typeof value, 'string', 'Checking: '+key);
    });

    var settingsToSet = {
        pinValue: '1234'
    };

    var result = VendorWifi.SetWebUiData( settingsToSet );
    ok(!Util.isErrorObject(result), 'SetWebUiData');

    settings = VendorWifi.GetWebUiData();
    validateObject(settings, settingsToSet );  // All should now be as we set them.
});

test('GetVodafoneConfiguration', function() {

    var settings = VendorWifi.GetVodafoneConfiguration();
    validateObject(settings, {
        sku: makeEnumerationChecker('Enterprise None P&P', 'Vodafone Pocket WiFi', 'Vodafone Mobile Wi-Fi'),
        maxConnectedDevices: isIntegerString
    }, 'GetRouterBackupUrl');
});

// *****************************************************************************************************
// Validation functions
// *****************************************************************************************************

test('GetParameterValidation values', function() {
    var vendorLimits = VendorWifi.GetParameterValidation();
    $.each(['maxPassword', 'maxSsid', 'maxWifiChannel', 'maxPortMappingApplicationName',
        'maxMacSettingsDescription'],
        function(index, str) {
            ok(typeof vendorLimits[str] === 'number', 'validation value: ' + str);
        }
    );
});

    test('GetParameterValidation functions', function() {
        var vl = VendorWifi.GetParameterValidation();
        ok(vl.validatePassword( 'a' ));
        ok(!vl.validatePassword( '%' ));
        ok(vl.validateSsid( '0' ));// – allows only specific characters.  / 0-9A-Za-z-_./.
        ok(!vl.validateSsid( '%' ));
        ok(vl.validateFolderName( 'a b' ));
        ok(!vl.validateFolderName( '/' ));

        ok(vl.validateEncryptionKey('deadbeef00', 'WEP'));
        ok(!vl.validateEncryptionKey('alkjf', 'WEP '));
        ok(vl.validateIpAddress('127.0.0.1'));
        ok(!vl.validateIpAddress('1.1.1'));
        ok(vl.validateSubnetMask('255.255.255.0'));
        ok(!vl.validateSubnetMask('255.255.255.9'));
        ok(!vl.validateSubnetMask('255.255.0'));
        ok(vl.validatePort('440'));
        //ok(!vl.validatePort('686868686'));  // FIXME This was an invalid test
        equals(vl.validatePort('686868686'), false, 'Port numbers must be less than 65536: 686868686');
        equals(vl.validatePort('0'), false, 'Port numbers must be more than 0: 0');
        equals(vl.validatePort('PETE'), false, 'Alpha port numbers are not valid: PETE');
        equals(vl.validatePort('pete10'), false, 'Alphanumeric port numbers are not valid: pete10');
        equals(vl.validatePort('10pete'), false, 'Alphanumeric port numbers are not valid: 10pete');
        ok(vl.validateMacAddress('01:01:01:01:01:01'));
        ok(!vl.validateMacAddress('Q:01:01:01:01:01'));
    });

// *****************************************************************************************************
// Checking the protocol of the API
// *****************************************************************************************************

test('Each call answers a different object (see Ownership in the spec)', function() {
   var result1 = VendorWifi.GetDmzSettings();
   var result2 = VendorWifi.GetDmzSettings();
   ok( result1 !== result2 );
});

asyncTest('Each async call answers a different object (see Ownership)', function() {
    expect(1);
    var result1;
    VendorWifi.GetDmzSettings(null, function(result) {
        result1 = result;
    });
    VendorWifi.GetDmzSettings( null, function(result){
        ok(result !== result1);
        start();
    });
});

    asyncTest("Login fail sends back sensible error object", function() {
        expect(4);
        VendorWifi.Login(
            {password: 'somethingElse'},
            $.noop,
            function LoginFail(error) {
                ok(Util.isErrorObject(error), 'got error object');
                equals(error.errorType, 'badPassword');
                equals(typeof error.errorId, 'string');
                equals(typeof error.errorText, 'string');
                start();
            });
    });

    asyncTest("Login success", function() {
    // Callback happens AFTER Login returns.
    var CalledBack = false;
    expect( 2 );
    VendorWifi.Login(
        {password: 'admin'},
        function LoginSuccess(result) {
            CalledBack = true;
            ok(result);
            start();
        }, function LoginFail(error) {
            ok(false, 'Unexpected login failure');
            start();
        });
    ok( !CalledBack, 'Callback must happen after Login returns');
});


asyncTest("Login parameter error", function() {
    expect(1);
    CallbackDestination.parameterError = function () {
        ok(true);
        start();
    };
    VendorWifi.Login({}, $.noop);
});

asyncTest("Logout success", function() {
    expect(1);
    VendorWifi.Logout(
        null,
        function done(result) {
            ok(result);
            start();
        });
});

    asyncTest("CheckUploadFileStatus - file upload success", function() {
        // Callback happens AFTER return.
        var CalledBack = false;
        expect(2);
        VendorWifi.CheckUploadFileStatus(
            {},
            function CheckUploadFileSuccess(result) {
                CalledBack = true;
                ok(result);
                start();
            }, function CheckUploadFileFail(error) {
                ok(false, 'Unexpected CheckUploadFileStatus failure');
                start();
            });
        ok(!CalledBack, 'Callback must happen after CheckUploadFileStatus returns');
    });

    asyncTest("CheckUploadFileStatus - file upload failure", function() {
        // Callback happens AFTER return.
        var CalledBack = false;
        expect(5);
        VendorWifi.CheckUploadFileStatus(
            {isErrorTest: true},  // indicate to the function that we are a test that expects failure
            function CheckUploadFileSuccess(result) {
                CalledBack = true;
                ok(false, 'Unexpected CheckUploadFileStatus success');
                start();
            }, function CheckUploadFileFail(error) {
                ok(Util.isErrorObject(error), 'got error object');
                equals(error.errorType, 'noSdCardPresent');
                equals(typeof error.errorId, 'string');
                equals(typeof error.errorText, 'string');
                start();
            });
        ok(!CalledBack, 'Callback must happen after CheckUploadFileStatus returns');
    });

// ****************************************************************************************
// * Test all the event functions
// ****************************************************************************************

var batteryStatus, wifiStatus, connectedDevices;
function setCallbacks() {
    batteryStatus = wifiStatus = connectedDevices = null;
    CallbackDestination.setBatteryStatus = function (status) {
        batteryStatus = status;
    };
    CallbackDestination.setWifiStatus = function (status) {
        wifiStatus = status;
    };
    CallbackDestination.setConnectedDevices = function (d) {
        connectedDevices = d;
    };
}

// Checks that all the callbacks have been called and have sensible results.
function validateCallbackObjects() {
    var i;
    ok(batteryStatus, 'batteryStatus');
    checkMatches(/^(charging|use)$/, batteryStatus.batteryStatus, 'batteryStatus');
    checkMatches(/^\d{1,3}$/, batteryStatus.batteryLevel, 'batteryLevel'); // Integer 0-100 as string.
    checkMatches(/^\d+$/, batteryStatus.batteryTime, 'batteryTime');  // Integer as string.

    ok(wifiStatus, 'wifiStatus');
    checkMatches(/^(enabled|disabled)$/, wifiStatus.wifiStatus, 'wifiStatus');
    checkMatches(/^.*$/, wifiStatus.ssid, 'ssid');  // Don't know rules for SSID
    checkMatches(/^(|NONE|WEP|AES|TKIP|[A-Z\/.]+)$/, wifiStatus.security, 'security'); // One of 'NONE', 'WEP' etc.

    ok(connectedDevices, 'connectedDevices');
    ok($.isArray(connectedDevices.devices), 'Device list is array');
    for (i = 0; i < 4; i++) {
        if (i < connectedDevices.devices.length) {
            equals(typeof connectedDevices.devices[i].deviceName, 'string', 'Must have device name: ' + i);
        } else {
            ok(true);  // Ensure we have the same number of assertions no matter how many devices.
        }
    }
}

asyncTest('Expect all the callbacks with sensible values', function () {
    expect(14);
    setCallbacks();
    VendorWifi.ForceCallbacks();
    setTimeout(function () {
        validateCallbackObjects();
        start();
    }, TimeBeforeAllCallbacksHaveHappenedMillis);
});

    // Extra testing for the dummy implementation requires we share some functionality
    return {
        setCallbacks: setCallbacks,
        validateCallbackObjects: validateCallbackObjects,
        CallbackDestination: CallbackDestination
    };
}());