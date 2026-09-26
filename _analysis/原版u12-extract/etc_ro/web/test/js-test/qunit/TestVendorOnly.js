/** Tests for Vendor API
 * (c) Penrillian Ltd 2012
 */

// Initial setup:
// SIM PIN must be 1234
var myPhoneNumber = '+12345678'; // Vendor please change for the SMS tests to the correct phone number.

// Many of the Vendor functions use a callback, so we use asyncTest.  Note that these tests will be ignored when
// using jsTestDriver (i.e. during build testing).


(function VendorOnlyTests() {
    var TimeBeforeAllCallbacksHaveHappenedMillis = 1000;

 module('VendorOnly', {
    setup: function () {

    },
    teardown: function () {
        Vendor.SetAPICallbackObject(window);
    }
});

test('Simple synchronous functions', function () {
    ok(makeEnumerationChecker('quickstart', 'mobilewifi')(GetWebUIProductName()));
    checkMatches(/[0-9 +]*/, GetPhoneNumber());
    checkMatches(/[0-9]+/, GetIMSIFromDevice() );
    checkMatches(/[0-9]+/, GetPinTimes() );
    checkMatches(/[0-9]+/, GetPUKTimes() );
    checkMatches(/[0-9]+/, GetRemainingUnlockNetworkAttempts() );
    checkMatches(/.+/, GetProductName() );
    checkMatches(/.+/, GetSoftwareVersion() );
    checkMatches(/.+/, GetHWVersion() );
    checkMatches(/.+/, GetSerialNumber());
    checkMatches(/.+/, GetSimSerialNumber() );
    checkMatches(/.+/, GetIMEI());
    ok(isBoolean(IsPINRequired()));
    equals(typeof GetSIMStatus(), 'number');
    ok(isBoolean(IsMessagePreview()));
    checkMatches(/[a-z]{2}.*/, GetLanguage());
    ok(makeEnumerationChecker(0,1)(GetNetworkAcquisitionMode()));
    var result = GetCustomAccountType();
    validateObject(result, {
        strAPN: isFullString,
        strNumber: isFullString,
        strDNS: isIpAddressString,
        strDNS2: isIpAddressString,
        strSecurity : isIntegerString,
        strUsername: isString,
        strPassword: isString
    });
    ok(makeEnumerationChecker('Contract', 'Prepaid', 'Business', 'Consumer', 'WebSession')(softwareAccountTypeFromHardwareAccountType(GetDefaultAccountType())));
    checkMatches(/.+/, GetApn() );
    equals(Supports('VENDOR_1_7'), true);
    equals(Supports('VENDORWIFI_1_7'), true);
    equals(Supports('DLNA_1_14'), true);
    equals(Supports('WPS_1_14'), true);
    equals(Supports('SOME_OTHER_VALUE'), false);
});

test('Other sync functions', function () {
    var nSMSType=1;
    for (; nSMSType<=11; nSMSType++) {
        equals( typeof GetSMSCount(nSMSType), 'number' );
    }

    var pn = '+447000123456';
    SavePhoneNumber(pn);
    equals( GetPhoneNumber(), pn );

    SetMessagePreview(false);
    ok(!IsMessagePreview());
    SetMessagePreview(true);
    ok(IsMessagePreview());
});

test('Sync SendUSSDAndGetResponse', function () {
    ok(!SendUSSDAndGetResponse());
    ok(!SendUSSDAndGetResponse(''));
    ok(!SendUSSDAndGetResponse('some string value'));
    ok(!SendUSSDAndGetResponse('some string value', null));
    ok(!SendUSSDAndGetResponse('some string value', 'some string value'));
    ok(!SendUSSDAndGetResponse(null, null));
    ok(!SendUSSDAndGetResponse(null, function() {}));

    ok(SendUSSDAndGetResponse('some string value', function() {}));
});

test('Sync GetRemainingUnlockNetworkAttempts', function () {
    ok(isInteger(GetRemainingUnlockNetworkAttempts()));
    ok(GetRemainingUnlockNetworkAttempts() >= 0);
});

// Functions where the callback has success boolean first
asyncTest( 'GetDataCounter', function() {
    GetDataCounter(function(success, result) {
        ok(success);
        validateObject(result, {
            nCurrentUploadRate: isInteger, 
            nCurrentDownloadRate: isInteger,
            nTotalUpload: isInteger,
            nTotalDownload: isInteger,
            nCurrentConnectTime: isInteger,
            nTotalConnectTime: isInteger,
            nTotalLifeTime: isInteger
        });
        start();
    });
});

asyncTest( 'GetCurrentNetwork', function() {
    GetCurrentNetwork(function(success, result) {
        ok(success);
        validateObject(result, {
            strFullName:isFullString,
            strShortName:isFullString,
            strNumeric:isIntegerString,
            nRat:isInteger,
            strBearer:isFullString
        });
        start();
    });
});

asyncTest( 'GetIPAddress', function() {
    GetIPAddress(function(success, result) {
        ok(success);
        ok(isFullString(result));
        ok(isIpAddressString(result));
        start();
    });
});

asyncTest( 'GetDNS', function() {
    GetDNS(function(success, result) {
        ok(success);
        ok(isFullString(result));
        ok(isIpAddressString(result));
        start();
    });
});

asyncTest( 'GetRoamingStatus', function() {
    GetRoamingStatus(function(success, result) {
        ok(success);
        ok(isBoolean(result) );
        start();
    });
});

asyncTest('Async SendUSSDAndGetResponse', function () {
    SendUSSDAndGetResponse('some string value', function(success, result) {
        ok(success);
        ok(isFullString(result));
        start();
    });
});

asyncTest( 'GetSupportedConnectionModes', function() {
    GetSupportedConnectionModes(function(bResult, vSupportedConnectionModes) {
        ok(bResult);
        validateObject(vSupportedConnectionModes, {
            bAutomatic: isBoolean,
            bManual: isBoolean,
            bOnDemand: isBoolean
        });
        start();
    });
});

asyncTest( 'Async UnlockNetwork', function() {
    var validUnlockNetworkCode = '1234';

    equals( GetRemainingUnlockNetworkAttempts(), 100 );
    UnlockNetwork('9999', function (bResult) {
        ok(!bResult);
        equals( GetRemainingUnlockNetworkAttempts(), 99 );
        UnlockNetwork('8888', function (bResult) {
            ok(!bResult);
            equals( GetRemainingUnlockNetworkAttempts(), 98 );
            UnlockNetwork('7777', function (bResult) {
                ok(!bResult);
                equals( GetRemainingUnlockNetworkAttempts(), 97 );
                UnlockNetwork(validUnlockNetworkCode, function (bResult) {
                    ok(bResult);
                    equals( GetRemainingUnlockNetworkAttempts(), 100 );
                    start();
                });
            });
        });
    });
});

asyncTest( 'GetConnectionMode', function() {
    GetConnectionMode(function(strConnectionMode, bAutoConnectWhenRoaming, success) {
        ok(success);
        ok(isBoolean(bAutoConnectWhenRoaming));
        ok(isIntegerString(strConnectionMode));
        SetConnectionMode('0', false, function (success) {
            ok(success,"Set Connection Mode");
            GetConnectionMode(function(strConnectionMode, bAutoConnectWhenRoaming, success) {
                ok(success);
                ok(!bAutoConnectWhenRoaming, 'roaming autoconnect off');
                equals(strConnectionMode, '0');
                SetConnectionMode('1', true, function (success) {
                    ok(success);
                    GetConnectionMode(function(strConnectionMode, bAutoConnectWhenRoaming, success) {
                        ok(success);
                        ok(bAutoConnectWhenRoaming, 'roaming on-demand on');
                        equals(strConnectionMode, '1');
                        SetConnectionMode('2', true, function (success) {
                            ok(success);
                            GetConnectionMode(function(strConnectionMode, bAutoConnectWhenRoaming, success) {
                                ok(success);
                                ok(bAutoConnectWhenRoaming, 'roaming on-demand on');
                                equals(strConnectionMode, '2');
                                start();
                            });
                        });
                    });
                });
            });
        });
    });
});

// Where the callback has success boolean second.
asyncTest( 'GetAccountType', function() {
    GetAccountType(function (result, success) {
        ok(success);
        checkMatches(/[a-zA-Z;]+/, result);
        start();
    });
});

asyncTest( 'GetBearerPreference returns allowed values', function() {
    GetBearerPreference(function (result, success) {
        ok(success);
        ok(makeEnumerationChecker("4G Preferred", "4G Only", "3G Preferred", "3G Only", "2G Only")(result));
        start();
    });
});

asyncTest('GetDateTime tests return valid internet date time', function(){

    GetDateTime(function(bResult,vDateTime) {
       ok(bResult);
       validateObject(vDateTime, {
            dateTime:isInternetDateTime
       });
       start();
    });
});

asyncTest( 'GetNetworkLocation tests return of valid location data', function() {
    GetNetworkLocation(function (bResult, vNetworkLocation ) {
        ok(bResult);
        validateObject(vNetworkLocation , {
            cellId: isInteger,
            rncId: isInteger,
            lac: isInteger
        });
        start();
    });
});

asyncTest( 'GetMessageCenter', function() {
    GetMessageCenter(function (result, success) {
        ok(success);
        checkMatches(/.+/, result);
        start();
    });
});

asyncTest( 'SetLanguage', function() {
    SetLanguage(function (success) {
        ok(success);
        equals(GetLanguage(), 'de-de');
        start();
    }, 'de-de');
});

asyncTest( 'Scan/select networks', function() {
    var networkDescriptionValidator = {
            strFullName: isFullString,
            strShortName: isFullString,
            strNumeric: isIntegerString,
            nRat: isInteger,
            nState: isInteger
        };

    ScanForNetwork(function (success, networks) {
        ok( makeObjectArrayValidator(networkDescriptionValidator, 'ScanForNetwork')(networks));
        ok( networks.length > 0);

        var connectNetwork = networks[0];
        SetNetwork(connectNetwork.strNumeric, connectNetwork.nRat, function (result) {
            validateObject(result, connectNetwork, 'SetNetwork');
            equals(result.nRat,connectNetwork.nRat,'nRat');
            equals(result.strNumeric,connectNetwork.strNumeric,'strNumeric');
            equals( GetNetworkAcquisitionMode(), 1, 'Network connect not automatic' );

            SetNetworkAcquisitionToAutomatic(function (result) {
                ok(result, 'SetNetworkAcquisitionToAutomatic');
                equals( GetNetworkAcquisitionMode(), 0, 'Network connect automatic' );
                start();
            });
        });
    });
});

//Archits
asyncTest('Set/Get Custom account type', function() {
    SetCustomAccountType('PP.INTERNET', '*99#', '192.168.0.1', '192.168.1.1', "3", "aUserName", "password",
        function(success) {
            ok(success, 'SetCustomAccountType');
            var customObject = GetCustomAccountType();
            equals(customObject.strAPN, 'PP.INTERNET');
            equals(customObject.strNumber, '*99#');
            equals(customObject.strDNS, '192.168.0.1');
            equals(customObject.strDNS2, '192.168.1.1');
            equals(customObject.strSecurity, '3');
            equals(customObject.strUsername, 'aUserName');
            equals(customObject.strPassword, 'password');
            start();
        });
});

asyncTest('Set AccountType',function() {
   SetAccountType('Prepaid', function(success) {
        ok(success,'Set Account Type Prepaid');
        equals('Prepaid',GetDefaultAccountType());
        start();
   });
});

asyncTest('Enable DoubleTapEnabled',function(){
    SetDoubleTapEnabled(true, function(success){
       ok(success);
       ok(IsDoubleTapEnabled());
       start();
    });
});

asyncTest('Disable DoubleTapEnabled',function(){
    SetDoubleTapEnabled(false, function(success){
       ok(success);
       ok(!IsDoubleTapEnabled());
       start();
    });
});

asyncTest('Get/Set Bearer Preference returns 4G Only',function(){
    SetBearerPreference('4G Only', function(success){
       ok(success);
       GetBearerPreference(function(strBearerPref, success){
           ok(success);
           equals('4G Only',strBearerPref);
           start();
       });
    });
});

asyncTest('Get/Set Bearer Preference returns 3G Only',function(){
    SetBearerPreference('3G Only', function(success){
       ok(success);
       GetBearerPreference(function(strBearerPref, success){
           ok(success);
           equals('3G Only',strBearerPref);
           start();
       });
    });
});

asyncTest('PIN Control',function(){
    // Get to a known state - PIN disabled.
    DisablePin('1234', function (ignore) {
        ok(!IsPINRequired());
        ok(!GetSimCardStatus().bPinState, 'GetSimCardStatus');
        EnablePin('1234','',function (success) {
            ok(success,'EnablePin');
            ok(IsPINRequired());
            ok(GetSimCardStatus().bPinState, 'GetSimCardStatus');
            EnablePin('1234','0000', function (success) {
                 ok(success,'EnablePin(change)');
                 ok(IsPINRequired());
                 EnablePin('0000','1234', function (success) {
                    ok(success,'EnablePin(change2)');
                    ok(IsPINRequired());
                     DisablePin('1234', function (success) {
                         ok(success);
                         ok(!IsPINRequired());
                         start();
                     });
                 });
            });
        });
    });
});

asyncTest('SMS message send', function () {
    function smsMessageReceived(count) {
        GetSMSMessages(1, 1, 10, function (result, success) {
            ok(success);
            ok(result.indexOf('Test QUNIT Send') !== -1);
            start();
        });
    }

    SetNewSMSCallbackFunction(smsMessageReceived);
    SendSMS(myPhoneNumber, 'Test QUNIT Send', function (success) {
        ok(success);
    }, -1);
});

asyncTest('Managing draft SMS messages', function () {
    var draftCount = GetSMSCount(4);
    SaveSMSMessage('Test Save', '07986544876', -1, function (success) {
        ok(success);
        equals(GetSMSCount(4), draftCount + 1, 'Right SMS count for draft');

        SaveSMSMessage('Test Save 2', '07986544876', -1, function (success) {
            ok(success);
            equals(GetSMSCount(4), draftCount + 2, 'Right SMS count for second add');
            GetSMSMessages(3, 1, 10, function (result, success) {
                ok(success);
                equals(typeof result, 'string');
                var messageArray = $.parseJSON(result).messages;
                ok(messageArray.length > 0);
                ok(messageArray.length <= 10);
                // First message should be the one we've just saved.
                equals(messageArray[0].body, 'Test Save');
                var idOfFirstMessage = messageArray[0].id;
                SetMessageRead(idOfFirstMessage, function (success) {
                    ok(success);
                    GetSMSMessages(3, 1, 10, function (result, success) {
                        ok(success);
                        var newMessageArray = $.parseJSON(result).messages;
                        equals(newMessageArray[0].id, idOfFirstMessage, 'Got same id for first message');
                        ok(!newMessageArray[0].isNew);
                        DeleteSMS(idOfFirstMessage, function (success) {
                            ok(success);
                            equals(GetSMSCount(4), draftCount + 1, 'Right SMS count for draft after delete');
                            DeleteMultipleSMS([messageArray[1].id], function (success) {
                                ok(success);
                                equals(GetSMSCount(4), draftCount, 'Right SMS count for draft after delete multiple');

                                start();
                            });
                        });
                    });
                });
            });
        });
    });
});

asyncTest('All async callbacks', function() {
    var callbackDestination = {};
    var result = {};
    var functionNames = ["getDataCounterCallback","setNetworkConnectionStatus","getNetworkCallback",
        "setPhoneNumber","setSignalStrength"];

    $.each(functionNames, function (ignore, funcName) {
        callbackDestination[funcName] = function () {
            result[funcName] = $.makeArray(arguments);
        };
    });
    Vendor.SetAPICallbackObject(callbackDestination);
    Vendor.ForceCallbacks();

    setTimeout(function () {
        // Check that all the callbacks have been called, and that the parameters were as expected:
        $.each(functionNames, function(ignore, funcName) {
            ok($.isArray(result[funcName]), funcName + ' was called');
        });
        ok(result.getDataCounterCallback[0], 'getDataCounterCallback result OK');

        validateObject(result.getDataCounterCallback[1], {
            nCurrentUploadRate: isInteger,
            nCurrentDownloadRate: isInteger,
            nTotalUpload: isInteger,
            nTotalDownload: isInteger,
            nCurrentConnectTime: isInteger,
            nTotalConnectTime: isInteger,
            nTotalLifeTime: isInteger
        }, 'getDataCounterCallback vDataCounter');
        var networkStatusChecker = makeEnumerationChecker('searchingForDevice', 'searchingForNetwork', 'connected',
            'connecting', 'disconnected', 'nodevice');
        ok(networkStatusChecker(result.setNetworkConnectionStatus[0]), 'setNetworkConnectionStatus');
        ok(result.getNetworkCallback[0], 'getNetworkCallback result');
        var networkInfo = result.getNetworkCallback[1];
        validateObject(networkInfo, {
            strFullName: isFullString,
            strShortName: isFullString,
            strNumeric: isIntegerString,
            nRat: isNumeric,
            strBearer: isFullString
        }, 'getNetworkCallback networkInfo');
        equals(typeof result.setPhoneNumber[0], 'string', 'setPhoneNumber');
        ok(makeEnumerationChecker('0', '1', '2', '3', '4', '5')(result.setSignalStrength[0]), 'setSignalStrength');

        start();
    }, TimeBeforeAllCallbacksHaveHappenedMillis);

});

    /*
    // Functions not tested.
    SetConnectionSettings(strIdleTime, strConnectionMode, bAutoConnectWhenRoaming, callback)  -- NOT USED

    EnterPin(strPinNumber, callback) -- side effects of changing SIM state.

    GetSMSStorageCapacityState(callback)

    Connect(callback)
    Disconnect(callback)
    RestoreFactorySettings(callback)
    RebootDevice()

    EnterPUK(strPUKNumber, strPinNumber, callback)

    SetMessageCenter(strMessageCenter, callback)
    ResetDataCounter(callback)



    VendorStart()
    */

}());