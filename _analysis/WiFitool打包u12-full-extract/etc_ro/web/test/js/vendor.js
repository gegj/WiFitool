/**
 * @author Vendor 
 */
var IMSI = '23415'; //example data
var Vendor = (function() { // Encapsulates all local variables and functions.

    //Please supply all SMS content in the following JSON format
    //var oneMessage = "{\"messages\":[ {\"id\": \"1\", \"date\": \"13/05/10\", \"time\": \"14:00\", \"from\": \"+447864629574\", \"body\": \"Hi James I just wanted to remind you about the meeting with our client\", \"isNew\": true}] }";

var apiCallbackObject = window;
var pinRequired = false;
var language = 'en-gb';
var strPhoneNumber = 'test phone';
var nAttemptsMax = 100;
var nAttempts = nAttemptsMax;
var nDraftSMSCount = 500;
var bearerPreference = '2G Only';
var strWebUIProductName = 'quickstart';
var doubleTapEnabled = true;
var timer;

/**************************************************************************
Function : GetRemainingUnlockNetworkAttempts
Description : Get the remaining number of unlock network attempts before lockdown
Parameters : void
return : nAttempts : Integer : an integer equal to or greater than zero
**************************************************************************/
function GetRemainingUnlockNetworkAttempts()
{
    return nAttempts;
}

/**************************************************************************
Function : UnlockNetwork
Description : Attempt to unlock network
Parameters : String : strUnlockNetworkCode : code used to attempt to unlock network
return : void
**************************************************************************/
function UnlockNetwork(strUnlockNetworkCode, callback)
{
    var result = false;
    if (strUnlockNetworkCode === '1234') {
        nAttempts = nAttemptsMax;
        result = true;
    } else {
        nAttempts--;
    }
    callback(result);
}

/**************************************************************************
Function : IsDoubleTapEnabled
Description : Get the user's double tap enabled setting. (Double tapping the power key will toggle to display of SSID and Wi-Fi key on the device OLED.)
Parameters : void
return : bDoubleTapEnabled : Boolean : true = double tap is enabled, false = double tap is disabled
**************************************************************************/
function IsDoubleTapEnabled()
{
    return doubleTapEnabled;
}

/**************************************************************************
Function : SetDoubleTapEnabled
Description : Set the user's double tap enabled setting. (Double tapping the power key will toggle to display of SSID and Wi-Fi key on the device OLED.)
Parameters : void
return : bDoubleTapEnabled : Boolean : true to enable the feature, false to disable it.
**************************************************************************/
function SetDoubleTapEnabled(bDoubleTapEnabled, callback)
{
    //Please map doubleTapEnabled to one of true or false and modify the device accordingly
    doubleTapEnabled = bDoubleTapEnabled;

    //Please map result to a boolean value to indicate success or failure
    var result = true;

    callback(result);
}

/**************************************************************************
Function : GetWebUIProductName
Description : Get WebUI product name; this is device specific and will be either ‘quickstart’ or ‘mobilewifi’.
Parameters : void
return : string : strWebUIProductName : WebUI product name [‘quickstart’|‘mobilewifi’]
**************************************************************************/
function GetWebUIProductName()
{
    return strWebUIProductName;
}

/**************************************************************************
Function : GetPhoneNumber
Description : get phone number from (1) any value passed in via SavePhoneNumber or (2) SIM Card phone number(Msisdn)
Parameters : void
return : string : strMsisdn : phone number, max-length = 31 Bytes.
**************************************************************************/
function GetPhoneNumber()
{
    //Vendor to call own API and populate strPhoneNumber with the phone number of the SIM
    return strPhoneNumber;
}

/**************************************************************************
Function : SavePhoneNumber
Description : Override the default SIM Card phone number
Parameters :
    [IN] string : strPhone : the phone number.
return: void
**************************************************************************/
function SavePhoneNumber(strPhone)
{
    strPhoneNumber = strPhone;
}

/**************************************************************************
 Function : GetCurrentNetwork
 Description : get current network information
 Parameters :
 [IN] : function :callback(bResult, vNetwork) : call back function, and the parameters list below:
 [IN] : bool   : bResult     : true = succeed, false = failed.
 [IN] : object : vNetwork : network information object, the object attribute list below:
 type   :   name       : description
 string : strFullName  : operator full name(the value is maybe ""),
 such as 'china mobile'
 string : strShortName : operator short name(the value is maybe ""),
 such as 'china mobile'
 string : strNumeric   : the digital number, such as '460'
 number : nRat         : the network connect technology, 0 = '2G', 2 = '3G'.
 string : strBearer   : the current bearer, maybe one of:
    <empty>
    GSM
    GPRS
    EDGE
    WCDMA
    HSDPA
    HSUPA
    HSPA
    TD_SCDMA
    HSPA+
    EVDO Rev.0
    EVDO Rev.A
    EVDO Rev.B
  if get current network information failed, the return value will be null.
 return : void
 **************************************************************************/
var indx = 0;
function GetCurrentNetwork(callback)
{
    // the object of network information
    var vNetwork = {strFullName:"UNICOM", strShortName:"UNICOM",strNumeric:"46009", nRat:2, strBearer:"EDGE" };
    var vNetwork2 = {strFullName:"UNICOM", strShortName:"UNICOM",strNumeric:"23415", nRat:2, strBearer:"HSDPA" };
    if(indx++%2 === 0)
    {
        vNetwork = vNetwork2;
    }
    callback(true, vNetwork);
}

/*function GetCurrentNetworkName() //New
 {
 var curNetwork = "test network";
 //Vendor to call own API and populate curNetwork with the current network.
 return curNetwork;
 }

 function GetMonitoringStatus()//New
 {
 var mStatus = "test mStatus";
 //Vendor to call own API and populate mStatus with the monitoring status.
 return mStatus;
 }*/

function GetIMSIFromDevice()
{
    //Vendor API call to retrieve IMSI from SIM and put in var IMSI as integer

    //no need for callback as we need this to continue forwards.
    return IMSI;
}

function GetPinTimes()
{
    var num = '2';
    //Vendor API call to retrieve number of remaining PIN attempts

    return num;
}

function GetPUKTimes()
{
    var num = '10';
    //Vendor API call to retrieve number of remaining PUK attempts

    return num;
}

function GetProductName()
{
    var ProductName = 'R210';
    //Vendor API call to retrieve ProductName and put result in var ProductName as string


    return ProductName;
}

function GetSoftwareVersion()
{
    var SoftwareVersion = 'test software version';
    //Vendor API call to retrieve SoftwareVersion and put result in var SoftwareVersion as string


    return SoftwareVersion;
}

function GetHWVersion()
{
    var HWVersion = 'test HW version';
    //Vendor API call to retrieve HW version and put result in var var HWVersion as string


    return HWVersion;
}

function GetSerialNumber()
{
    var SerialNumber = 'test serial number';
    //Vendor API call to retrieve Serial Number and put result in var SerialNumber as string


    return SerialNumber;
}

function GetSimSerialNumber() //Serial number of sim (ICCID)
{
    var SimSerialNumber = 'test 867867856786894684';
    //Vendor API call to retrieve Sim Serial Number and put result in var SerialNumber as string


    return SimSerialNumber;
}

/**************************************************************************
Function : GetDataCounter
Description : get flux information
Parameters :
    [IN] : function : callback(bResult, vResponse) : call back function, and the parameters list below:
        [IN] : bool : bResult : true = succeed, false = failed.
        [IN] : object : vResponse : it is an object variable containing the flux information, the attributes list below.
               if failed, we will return null.
                 type : name : description
                number : nCurrentConnectTime : current connection time(unit : second).
                number : nCurrentUploadRate : current uplink speed(unit : byte/second).
                number : nCurrentDownloadRate: current downlink speed(unit : byte/second).
                number : nTotalUpload : total upload flux(unit : byte).
                number : nTotalDownload : total download flux(unit : byte).
                number : nTotalConnectTime : total connection time(unit : second).
                number : nTotalLifeTime : total time device has been powered up(unit : second).
return : void
**************************************************************************/
function GetDataCounter(callback)
{
    //get data-counter information to the object
    var vDataCounter = {};
    vDataCounter.nCurrentUploadRate = 2;
    vDataCounter.nCurrentDownloadRate = 3;
    vDataCounter.nTotalUpload = 4;
    vDataCounter.nTotalDownload = 5;
    vDataCounter.nCurrentConnectTime = 6;
    vDataCounter.nTotalConnectTime = 7;
    vDataCounter.nTotalLifeTime = 8;

    callback(true, vDataCounter);
}

var currentNetworkConnectionStatus = "connected";

function TimerCallbackFunction()
{
	//g_fnNewSMSCallback(2);
    if (Util.inUnitTest() && apiCallbackObject === window) {
        return;
    }
    
    GetCurrentNetwork( function (result, networkInfo) {
        apiCallbackObject.getNetworkCallback(result, networkInfo);
    });
    GetDataCounter(function (result, vDataCounter) {
        apiCallbackObject.getDataCounterCallback(result, vDataCounter);
    });
    apiCallbackObject.setNetworkConnectionStatus(currentNetworkConnectionStatus); // input param string containing one of 'searchingForDevice', 'searchingForNetwork', 'connected', 'connecting', 'disconnected', 'nodevice', 'limitedService'
    apiCallbackObject.setPhoneNumber(GetPhoneNumber());
    apiCallbackObject.setSignalStrength(Math.floor(Math.random() * 6).toString());
}

function GetIMEI()
{
    var IMEI = 'test IMEI';
    //Vendor API call to retrieve IMEI and put result in var IMEI as string


    return IMEI;
}

/**************************************************************************
Function : IsPINRequired
Description : get the status of the PIN code
Parameters : void
return : nIsPINRequired
nIinIsRequired == true means that a PIN is required in order to use the device.

This function is fast and synchronous.
**************************************************************************/
function IsPINRequired()
{
     //Vendor code must retrieve setting and return it as defined above.
   
   return pinRequired;
}

/**************************************************************************
 Function : GetSIMStatus
 Description : get sim status
 Parameters : void
 return : number : nSimStatus : get SIM card Status, the value list below:
 1 = 'Ready',
 2 = 'PIN Required',
 3 = 'PUK Required',
 4 = 'no sim card or invalid card',
 if get sim status failed the return value will be 0(0 = failed).
 **************************************************************************/
function GetSIMStatus()
{
    var SIMStatus = 2;
    //Vendor API call to retrieve SIM Status and return int as defined above
    return SIMStatus;
}

function GetIPAddress(callback)
{
    var result = true; //use this to store the success/failure of the API request and return in the callback
    var IPAddress = '1.2.3.4';
    // Vendor API call to retrieve device IPAddress and put result in var IPAddress as string
    //Callback requires a boolean value to indicate success or failure
    callback(result, IPAddress);
}

function GetDNS(callback)
{
    var result = true; //use this to store the success/failure of the API request and return in the callback
    var DNS = '192.168.1.1';
    // Vendor API call to retrieve device DNS and put result in var DNS as string

    //Callback requires a boolean value to indicate success or failure
    callback(result, DNS);
}

// Function to set the ISO-3166 language string.
// Parameters :
//   [IN] : function : callback : callback function.
//   [IN] : String : strLanguageString : The ISO-3166 String to set as the langauge.
function SetLanguage(callback, strLanguageString)
{
    language = strLanguageString;

    var result = true; //use this to store the success/failure of the API request and return in the callback


    // Vendor API call to set the language (persistent storage).  This will be retrieved at a later time

    //Callback requires a boolean value to indicate success or failure
    callback(result);
}

var isMessagePreviewValue = true;
// Function to return the user's preferred message preview setting.
function IsMessagePreview()
{
    return isMessagePreviewValue;
}

// Function to save the user's preferred message preview setting.
// Parameters :
//   [IN] : boolean : isMessagePreview : true - user wishes to see the messages previewed.  False - they do not.
function SetMessagePreview(isMessagePreview)
{
    // Vendor API call to save boolean in persistent storage indicating whether to preview messages on first page.
    isMessagePreviewValue = isMessagePreview;
}

// Function to get the ISO-3166-language string.
// Parameters :
//   [OUT] : String : The ISO-3166 langauge string if any.  If none then "".
function GetLanguage()
{
    return language;
}

/**************************************************************************
 Function : ScanForNetwork
 Description : scan the network
 Parameters :
 [IN] : function :callback(bResult, listNetwork) : call back function, and the parameters list below:
 [IN] : bool   : bResult     : true = succeed, false = failed.
 [IN] : object : listNetwork : network information array, the object attribute in the array below:
 type   :   name                   : description
 string : strFullName              : operator full name(the value is maybe ""),
 such as 'china mobile'
 string : strShortName             : operator short name(the value is maybe ""),
 such as 'china mobile'
 string : strNumeric               : the digital number, such as '460'
 number : nRat                     : the network connect technology, 0 = '2G', 2 = '3G'.
 number : nState : operator availability as int at+cops=? <stat> (This is as per 3GPP TS 27.007)
 if get net work list failed, the return value will be an null array.
 return : void
 **************************************************************************/
function ScanForNetwork(callback)
{
    // the array are all of net work information
    var listNetwork = [];

    var vNetwork1 = {};
    vNetwork1.strFullName = "UNICOM";
    vNetwork1.strShortName = "UNICOM";
    vNetwork1.strNumeric = "46009";
    vNetwork1.nRat = 0;
    vNetwork1.nState = 2;

    listNetwork[0] = vNetwork1;

    var vNetwork2 = {};
    vNetwork2.strFullName = "CHN09 REALLY REALLY LONG";
    vNetwork2.strShortName = "CHN09 REALLY REALLY LONG";
    vNetwork2.strNumeric = "46008";
    vNetwork2.nRat = 2;
    vNetwork2.nState = 1;

    listNetwork[1] = vNetwork2;

    var vNetwork3 = {};
    vNetwork3.strFullName = "Vodafone UK";
    vNetwork3.strShortName = "voda";
    vNetwork3.strNumeric = "23415";
    vNetwork3.nRat = 2;
    vNetwork3.nState = 3;

    listNetwork[2] = vNetwork3;

    callback(true, listNetwork);
}

/**************************************************************************
 Function : SetNetwork
 Description : set current network
 Parameters :
 [IN] : string   : strNetworkNumber : the network digital number MCCMNC.
 [IN] : number   : nRat : the network connect technology: 0 = "2G", 2 = "3G".
 [IN] : function : callback(bResult) : call back function, and the parameters list below:
 [IN] : bool : bResult : true = succeed, false = failed.
 return : bool : if the parameters is invalid, the function will return false, otherwise will return true.
 comment: we need another parameter nRat, the value may be: 0 = '2G' or 2 = '3G'.
 **************************************************************************/
var networkAcquisitionMode = 1; // Manual
function SetNetwork(strNetworkNumber, nRat, callback)
{
    if((typeof(strNetworkNumber) !== "string") || (strNetworkNumber === "") ||
        (typeof(nRat) !== "number") || (isNaN(nRat)))
    {
        if(typeof(callback) === "function")
        {
            callback(null);
            return;
        }
    }

    var nRat1 = -1;

    if(nRat === 0)
    {
        // 0 = '2G'
        nRat1 = 0;
    }
    else if(nRat === 2)
    {
        // 2 = '3G'
        nRat1 = 2;
    }
    else
    {
        // -1 = ''
        nRat1 = -1;
    }

    if(-1 === nRat1)
    {
        if(typeof(callback) === "function")
        {
            callback(null);
        }
    }

    setTimeout(function() {
       // do the work......
    networkAcquisitionMode = 1; // Manual
    var vNetwork = {};
    vNetwork.strFullName = "UNICOM";
    vNetwork.strShortName = "UNICOM";
    vNetwork.strNumeric = strNetworkNumber;
    vNetwork.nRat = nRat;
    vNetwork.nState = 2;
    callback(vNetwork);
    }, 5000);
}


/**************************************************************************
 Function : SetNetworkAcquisitionToAutomatic
 Description : sets the network acquisition to automatic.  Note this must persist through power down and restart of the device
 Parameters :
 [IN] : function :callback(vNetwork) : call back function

 type   :   name       : description
 string : strFullName  : operator full name(the value is maybe ""),
 such as 'china mobile'
 string : strShortName : operator short name(the value is maybe ""),
 such as 'china mobile'
 string : strNumeric   : the digital number, such as '23415' (MCCMNC)
 number : nRat         : the network connect technology, 0 = '2G', 2 = '3G'.
 number : nMode        : the network mode defines whether the network acquisition is done automatically or it is forced by this command to operator as part of at+cops? (3GPP TS 27.007), 0 is automatic, 1 is manual
 if SetNetworkAcquisitionToAutomatic failed, the network in the callback value will be null.
 return : void
 **************************************************************************/

function SetNetworkAcquisitionToAutomatic(callback)
{
    // do the work to set the acquisition to be automatic
    setTimeout(function() {
        networkAcquisitionMode = 0;
        callback(true);
    }, 5000);
}

/**************************************************************************
Function : GetNetworkAcquisitionMode
Description : get search network mode
Parameters : void
return : number : nMode : 0 = 'automatic', 1 = 'manual'
**************************************************************************/
function GetNetworkAcquisitionMode()
{
    return networkAcquisitionMode;
}

function SendSMSAndGetResponse(strNumber, strMessageBody, strIdentifier, callback)
{

    //Please supply all SMS content in the following JSON format
    //var oneMessage = "{\"messages\":[ {\"id\": \"1\", \"date\": \"13/05/10\", \"time\": \"14:00\", \"from\": \"+447864629574\", \"body\": \"Hi James I just wanted to remind you about the meeting with our client\", \"isNew\": true}] }";

    var result = true; //use this to store the success/failure of the API request and return in the callback
    var response = "{\"messages\":[ {\"id\": \"1\", \"date\": \"13/05/10\", \"time\": \"14:00\", \"from\": \"+447864629574\", \"body\": \"Jumeli € 23.05\", \"isNew\": true}] }";
    //Vendor API call to send SMS and wait for reply SMS

    //strIdentifier is an array of SMS senders. eg. shortcode or operator string
    //Only callback once an SMS has been recieved from one of the senders in the identifiers array.
    //Callback with the SMS content identified above.

    //This will be used to send data to the network, and we expect an SMS to be sent back to confirm.
    //This function needs to wait until we get an SMS response from a sender which is in the strIdentifiers

    setTimeout(function(){
        //Callback requires a boolean value to indicate success or failure along with the SMS message response
        callback(result, response);
    }, 5000);
}


/**************************************************************************
 Function: GetSMSMessages
 Description: get sms messages information
 Parameters:
 [IN] : number : nMessageStoreType, you can select following value:
 type : meaning

 NOTE: THESE NUMBERS ARE DIFFERENT FROM GetSMSCount.  SORRY!

 1 : 'local inbox'
 2 : 'local outbox'
 3 : 'local draftbox'
 4 : 'local dustbin'
 5 : 'SIM inbox'
 6 : 'SIM outbox'
 7 : 'SIM draftbox'
 [IN] : number : nPageNum : SMS UI page index.
 [IN] : number : nNumberMessagesPerPage : an page read count, the value can between [1, 50].
 [IN] : function : callback(listMessagesJSON, bResult) : callback function
 [IN] : object : listMessagesJSON : message JSON object list. Each message JSON object contains all messages information as below.
 if failed, we will return null.
 an message JSON object such as vMessagesJSON, this vMessagesJSON.messages[0]. its attributes lists below :

 type : atrributes : Description : such as
 string : id : sms Index : '1'
 string : date : Year/month/day : '13/05/10'
 string : time : hours : '14:00'
 string : from : ministry people phone : '+447864629574'
 string : body : sms content : 'Hi James!'
 bool : isNew : if isNew=true the sms is New, otherwise the sms is Old

 [IN] : bool : bResult : true = succeed , false = failed
 return: bool : true = function execute succeed, false = parameters is invalid
 **************************************************************************/
var messageArr1 = "{\"messages\":[ {\"id\": \"11\", \"date\": \"13/05/10\", \"time\": \"14:00\", \"from\": \"+447864629574\", \"body\": \"Test Save\", \"isNew\": false}, {\"id\": \"11\", \"date\": \"13/05/10\", \"time\": \"14:00\", \"from\": \"+447864629574\", \"body\": \"<\\\"/''\\\"\\\\>\", \"isNew\": true},{\"id\": \"2\", \"date\": \"11/04/10\", \"time\": \"09:07\", \"from\": \"Vodafone\", \"body\": \"\", \"isNew\": true},{\"id\": \"3\", \"date\": \"09/04/10\", \"time\": \"07:37\", \"from\": \"+447864629574\", \"body\": \"Hi there. Are you going to make the meeting at 2pm? This is a test of the 160 character limit and breaking over two lines for a message\", \"isNew\": true},{\"id\": \"4\", \"date\": \"03/03/10\", \"time\": \"17:54\", \"from\": \"Vodafone Roaming\", \"body\": \"Welcome to Ireland. You are connected to Vodafone IRL.  (Test QUNIT Send)\", \"isNew\": true},{\"id\": \"5\", \"date\": \"09/02/10\", \"time\": \"21:46\", \"from\": \"Vodafone\", \"body\": \"Your monthly limit is 3Gb. We will text you when you reach this\", \"isNew\": true},{\"id\": \"6\", \"date\": \"09/02/10\", \"time\": \"21:46\", \"from\": \"+447864629574\", \"body\": \"Did you manage to get those wire frames over to the build team?\", \"isNew\": true},{\"id\": \"7\", \"date\": \"09/02/10\", \"time\": \"21:46\", \"from\": \"+447864629574\", \"body\": \"Hey Carl this is another test of the 140 and 160 character limit and how it should be dealt with in the inbox breaking over two lines seems to work\", \"isNew\": true}] }";
var resGetSMS   = true;       //used to simulate deleting all messages locally
function GetSMSMessages(nMessageStoreType, nPageNum, nNumberMessagesPerPage, callback)
{
    //Please supply all SMS content in the following JSON format
    //var oneMessage = "{\"messages\":[ {\"id\": \"1\", \"date\": \"13/05/10\", \"time\": \"14:00\", \"from\": \"+447864629574\", \"body\": \"Hi James I just wanted to remind you about the meeting with our client\", \"isNew\": true}] }";
    

    var result = true; //use this to store the success/failure of the API request and return in the callback
    var messageArr2 = "{\"messages\":[ {\"id\": \"21\", \"date\": \"13/05/10\", \"time\": \"14:00\", \"from\": \"+447864629574\", \"body\": \"2 Hi James I just wanted to remind you about the meeting with our client\", \"isNew\": true},{\"id\": \"2\", \"date\": \"11/04/10\", \"time\": \"09:07\", \"from\": \"Vodafone\", \"body\": \"Your latest bill is ready to view in your online account area\", \"isNew\": true},{\"id\": \"3\", \"date\": \"09/04/10\", \"time\": \"07:37\", \"from\": \"+447864629574\", \"body\": \"Hi there. Are you going to make the meeting at 2pm? This is a test of the 160 character limit and breaking over two lines for a message\", \"isNew\": true},{\"id\": \"4\", \"date\": \"03/03/10\", \"time\": \"17:54\", \"from\": \"Vodafone2 Roaming\", \"body\": \"Welcome to Ireland. You are connected to Vodafone IRL\", \"isNew\": true},{\"id\": \"5\", \"date\": \"09/02/10\", \"time\": \"21:46\", \"from\": \"Vodafone\", \"body\": \"Your monthly limit is 3Gb. We will text you when you reach this\", \"isNew\": true},{\"id\": \"6\", \"date\": \"09/02/10\", \"time\": \"21:46\", \"from\": \"+447864629574\", \"body\": \"Did you manage to get those wire frames over to the build team?\", \"isNew\": true},{\"id\": \"7\", \"date\": \"09/02/10\", \"time\": \"21:46\", \"from\": \"+447864629574\", \"body\": \"Hey Carl this is another test of the 140 and 160 character limit and how it should be dealt with in the inbox breaking over two lines seems to work\", \"isNew\": true}] }";
    var messageArr3 = "{\"messages\":[ {\"id\": \"31\", \"date\": \"13/05/10\", \"time\": \"14:00\", \"from\": \"+447864629574\", \"body\": \"3 Hi James I just wanted to remind you about the meeting with our client\", \"isNew\": true},{\"id\": \"2\", \"date\": \"11/04/10\", \"time\": \"09:07\", \"from\": \"Vodafone\", \"body\": \"Your latest bill is ready to view in your online account area\", \"isNew\": true},{\"id\": \"3\", \"date\": \"09/04/10\", \"time\": \"07:37\", \"from\": \"+447864629574\", \"body\": \"Hi there. Are you going to make the meeting at 2pm? This is a test of the 160 character limit and breaking over two lines for a message\", \"isNew\": true},{\"id\": \"4\", \"date\": \"03/03/10\", \"time\": \"17:54\", \"from\": \"Vodafone3 Roaming\", \"body\": \"Welcome to Ireland. You are connected to Vodafone IRL\", \"isNew\": true},{\"id\": \"5\", \"date\": \"09/02/10\", \"time\": \"21:46\", \"from\": \"Vodafone\", \"body\": \"Your monthly limit is 3Gb. We will text you when you reach this\", \"isNew\": true},{\"id\": \"6\", \"date\": \"09/02/10\", \"time\": \"21:46\", \"from\": \"+447864629574\", \"body\": \"Did you manage to get those wire frames over to the build team?\", \"isNew\": true},{\"id\": \"7\", \"date\": \"09/02/10\", \"time\": \"21:46\", \"from\": \"+447864629574\", \"body\": \"Hey Carl this is another test of the 140 and 160 character limit and how it should be dealt with in the inbox breaking over two lines seems to work\", \"isNew\": true}] }";
    var messageArr4 = "{\"messages\":[ {\"id\": \"41\", \"date\": \"13/05/10\", \"time\": \"14:00\", \"from\": \"+447864629574\", \"body\": \"4 Hi James I just wanted to remind you about the meeting with our client\", \"isNew\": true},{\"id\": \"2\", \"date\": \"11/04/10\", \"time\": \"09:07\", \"from\": \"Vodafone\", \"body\": \"Your latest bill is ready to view in your online account area\", \"isNew\": true},{\"id\": \"3\", \"date\": \"09/04/10\", \"time\": \"07:37\", \"from\": \"+447864629574\", \"body\": \"Hi there. Are you going to make the meeting at 2pm? This is a test of the 160 character limit and breaking over two lines for a message\", \"isNew\": true},{\"id\": \"4\", \"date\": \"03/03/10\", \"time\": \"17:54\", \"from\": \"Vodafone4 Roaming\", \"body\": \"Welcome to Ireland. You are connected to Vodafone IRL\", \"isNew\": true},{\"id\": \"5\", \"date\": \"09/02/10\", \"time\": \"21:46\", \"from\": \"Vodafone\", \"body\": \"Your monthly limit is 3Gb. We will text you when you reach this\", \"isNew\": true},{\"id\": \"6\", \"date\": \"09/02/10\", \"time\": \"21:46\", \"from\": \"+447864629574\", \"body\": \"Did you manage to get those wire frames over to the build team?\", \"isNew\": true},{\"id\": \"7\", \"date\": \"09/02/10\", \"time\": \"21:46\", \"from\": \"+447864629574\", \"body\": \"Hey Carl this is another test of the 140 and 160 character limit and how it should be dealt with in the inbox breaking over two lines seems to work\", \"isNew\": true}] }";
    var emptyMessageArr = [];
    //Vendor API call to retrieve messages from device.
    //messageArr must contain the new messages in the format specified at the beginning of this file

    //Callback requires a boolean value to indicate success or failure

    if(nPageNum === 1)
    {
        callback(resGetSMS ? messageArr1 : emptyMessageArr, result);
        messageArr1 = "{\"messages\":[ {\"id\": \"11\", \"date\": \"13/05/10\", \"time\": \"14:00\", \"from\": \"+447864629574\", \"body\": \"Test Save\", \"isNew\": false}, {\"id\": \"11\", \"date\": \"13/05/10\", \"time\": \"14:00\", \"from\": \"+447864629574\", \"body\": \"" + "<>&bbcc><)([]{+};:/~.,!#~|_)£@€$¥#*=+%" + "\", \"isNew\": true},{\"id\": \"2\", \"date\": \"11/04/10\", \"time\": \"09:07\", \"from\": \"Vodafone\", \"body\": \"\", \"isNew\": true},{\"id\": \"3\", \"date\": \"09/04/10\", \"time\": \"07:37\", \"from\": \"+447864629574\", \"body\": \"Hi there. Are you going to make the meeting at 2pm? This is a test of the 160 character limit and breaking over two lines for a message\", \"isNew\": true},{\"id\": \"4\", \"date\": \"03/03/10\", \"time\": \"17:54\", \"from\": \"Vodafone1 Roaming\", \"body\": \"Welcome to Ireland. You are connected to Vodafone IRL.  (Test QUNIT Send)\", \"isNew\": true},{\"id\": \"5\", \"date\": \"09/02/10\", \"time\": \"21:46\", \"from\": \"Vodafone\", \"body\": \"Your monthly limit is 3Gb. We will text you when you reach this\", \"isNew\": true},{\"id\": \"6\", \"date\": \"09/02/10\", \"time\": \"21:46\", \"from\": \"+447864629574\", \"body\": \"Did you manage to get those wire frames over to the build team?\", \"isNew\": true},{\"id\": \"7\", \"date\": \"09/02/10\", \"time\": \"21:46\", \"from\": \"+447864629574\", \"body\": \"Hey Carl this is another test of the 140 and 160 character limit and how it should be dealt with in the inbox breaking over two lines seems to work\", \"isNew\": true}] }";
//        resGetSMS = true;
    } else if(nPageNum === 2)
    {
        callback(messageArr2, result);
    } else if(nPageNum === 3)
    {
        callback(messageArr3, result);
    } else
    {
        callback(messageArr4, result);
    }
}

/*
* [IN] : number : nIndex, message index, there are two situations list below:
* if nIndex=-1, it means that webUI wants to save a new message to local draftbox;
* if nIndex>-1, it means that webUI wants to modify a message which has already saved in local draftbox.
* string : strSMSMessage, message to be saved
* [IN] : string : strSMSNumber, number to which the message may be sent
* [IN] : function : saveSMSMessageCallback(bResult) : callback function
* [IN] : bool   : bResult     : true = succeed , false = failed
*/
function SaveSMSMessage(strSMSMessage, strSMSNumber, nIndex, saveSMSMessageCallback)
{
    nDraftSMSCount = nDraftSMSCount + 1;
    saveSMSMessageCallback(true);
}
/*
Function: SetNewSMSCallbackFunction
Description: set new sms callback function(vodafone must be transfer the function when page load)
Parameters:
[IN] : function : callback(g_nNewSMSNumber) : callback function, the parameters list below:
[IN] : number : g_nNewSMSNumber : new sms count.
return: void */
function SetNewSMSCallbackFunction(callbackReceiveNewSMS)
{
	if ("function" === typeof(callbackReceiveNewSMS))
	{
		g_fnNewSMSCallback = callbackReceiveNewSMS;
	}
} 

/**************************************************************************
 Function: GetSMSCount
 Description: get message count
 Parameters:
 [IN] : number : nSMSType, you can select following value:
 type : meaning
 1  : 'local unread'
 2  : 'local inbox'
 3  : 'local outbox'
 4  : 'local draft'
 5  : 'local dustbin'
 6  : 'SIM unread'
 7  : 'SIM inbox'
 8  : 'SIM outbox'
 9  : 'SIM draftbox'
 10 : 'local max storage'
 11 : 'SIM max storage'

 return: number : nSMSCount : Messages Count.
 if this function failed, the return value is -1.
 **************************************************************************/
function GetSMSCount(nSMSType)
{
     var nSMSCount;
    if(nSMSType===3) {
        nSMSCount = 101;
    }
    else if(nSMSType===4) {
        nSMSCount = nDraftSMSCount;
    }
    else {
        nSMSCount = 37;
    }

    return nSMSCount;
}

function GetSMSStorageCapacityState(callback)
{
    var result =  true;
    var vCapacity = {};
    vCapacity.nUsed = 80; // the count of SMS stores in device
    vCapacity.nTotal = 1000; // the total count of SMS storage in device
    callback(result, vCapacity);
}
function SendSMS(strNumber, strMessageBody, callback, strID)
{
    var isUnitTesting = strMessageBody.indexOf('Test QUNIT Send') !== -1;
    var timeToSend = isUnitTesting ? 0: 5000; //ms
    var result = true; //use this to store the success/failure of the API request and return in the callback
    //Vendor API call to send SMS using strNumber as recipient number, strMessageBody as message text (both strings).
    //Callback requires a boolean value to indicate success or failure
    setTimeout(function() {
        callback(result);
        if (isUnitTesting) {
            g_fnNewSMSCallback(10); // Always sending to ourself!
        }
    }, timeToSend);
}

function DeleteSMS(ID, callback)
{
    var result = true; //use this to store the success/failure of the API request and return in the callback
    //Vendor API call to delete SMS using ID provided
    nDraftSMSCount--;

    //Callback requires a boolean value to indicate success or failure
    callback(result, ID);
}

function DeleteMultipleSMS(IDs, callback) //new
{
    var result = true; //use this to store the success/failure of the API request and return in the callback
    //Vendor API call to delete SMS using the array if IDs provided
    //IDs is an Array of IDs to delete
    nDraftSMSCount -= IDs.length;
    messageArr1 = "";
    resGetSMS = false;

    //Callback requires a boolean value to indicate success or failure
    callback(result, IDs);
}

function SendUSSDAndGetResponse(strUSSDCommand, callback)
{
    var response2= 'TopUp unsuccessful.Invalid number';
    if ( ('string' !== typeof(strUSSDCommand)) || ('' === strUSSDCommand) ) {
        return false;
    }
    if (!$.isFunction(callback)) {
        return false;
    }
    var result = true; //use this to store the success/failure of the API request and return in the callback
    var response = "";
    if (strUSSDCommand.indexOf('BAD') !== -1) { // a way to force an error return
        result = false;
    } else if (strUSSDCommand.indexOf('unsuccessful') !== -1) { // a way to force an error return
        result = true;
        response = response2;
    } else if (strUSSDCommand.indexOf('*#135') !== -1) { // a way to force an error return
      response = "DSVSDVBS #18.45"  ;
    }
    else{
    // Warning - this is also used for Prepay - where this hard coded response won't make much sense.
        response = 'TopUP successful#33.85';
    }
    setTimeout(function(){
        //Vendor API call
        //Callback requires a boolean value to indicate success or failure along with the USSD response
        callback(result, response);
    }, 5000);

    return true;
}

function Connect(callback)
{
    currentNetworkConnectionStatus = "connecting";
    setTimeout( function () {
        var result = true; //use this to store the success/failure of the API request and return in the callback
        //Vendor API call to connect data connection
        currentNetworkConnectionStatus = "connected";
        callback(result);
    }, 5000);
}

function Disconnect(callback)
{
    writeToConsole('Disconnect');
    var result = true; //use this to store the success/failure of the API request and return in the callback
    //Vendor API call to Disconnect data connection
    setTimeout(function()
    {
        currentNetworkConnectionStatus = "disconnected";
        //Callback requires a boolean value to indicate success or failure
        callback(result);
    }, 1000);
}

function EnterPin(strPinNumber, callback)
{
    var result = false; //use this to store the success/failure of the API request and return in the callback
    //Vendor API call to set Pin number for SIM card
    // Note that device vendor will return false once the user has put in the PIN successfully;
    // so this function cannot be used just for PIN validation.
    if (strPinNumber === '1234') {
        result = true;
    }
    //Callback requires a boolean value to indicate success or failure
    callback(result);
}

function EnablePin(strOldPinNumber, strNewPinNumber, callback)
{
    var result = true; //use this to store the success/failure of the API request and return in the callback
    //Vendor API call to enable Pin number for SIM card
    pinRequired = true;
    //Callback requires a boolean value to indicate success or failure
    callback(result);
}

function DisablePin(strOldPinNumber, callback)
{
    var result = true; //use this to store the success/failure of the API request and return in the callback
    //Vendor API call to disable Pin number for SIM card
    pinRequired = false;
    //Callback requires a boolean value to indicate success or failure
    callback(result);
}

function EnterPUK(strPUKNumber, strPinNumber, callback)
{
    var result = false; //use this to store the success/failure of the API request and return in the callback
    if (strPUKNumber === '1234') {
        result = true;
    }
    //Vendor API call to set Pin number for SIM card
    //Callback requires a boolean value to indicate success or failure
    callback(result);
}

/**************************************************************************
Function: GetSupportedConnectionModes
Description: Get supported connection modes
Parameters:
[IN] : function : callback(vSupportedConnectionModes, bResult) : callback function with the parameters listed below:
  [IN] :bool : bAutomatic  : true if automatic mode is supported
  [IN] :bool : bManual  : true if manual mode is supported
  [IN] :bool : bOnDemand  : true if on-demand mode is supported
[IN] : bool : bResult : true = succeed, false = failed.
return: void
**************************************************************************/

function GetSupportedConnectionModes(callback)
{
    var bResult = true;
    var vSupportedConnectionModes = {
        bAutomatic : true,
        bManual : true,
        bOnDemand : true
    };

    callback(bResult, vSupportedConnectionModes);
}

/**************************************************************************
 Function: GetConnectionMode
 Description: get connection mode
 Parameters:
 [IN] : function : callback(strConnectionMode, bAutoConnectWhenRoaming, bResult) : call back function, and the parameters list below:
 [IN] : string : strConnectionMode : connection mode, '0' indicates automatic, '1' indicates manual, '2' indicates on-demand.
 if get connection mode failed, we will return "".
 [IN] : bool : bAutoConnectWhenRoaming : whether the device should auto connect when roaming
 false = forbidden, true = open.
 [IN] : bool : bResult : true = succeed, false = failed.
 return: void
 **************************************************************************/
var connectionMode = '0';
var autoConnectWhenRoaming = true;
function GetConnectionMode(callback)
{
    callback(connectionMode, autoConnectWhenRoaming, true);
}

/**************************************************************************
 Function : SetConnectionMode
 Description : set connection mode
 Parameters :
 [IN] : string : strConnectionMode : connection mode, such as:
 '0' indicates automatic, '1' indicates manual, '2' indicates on-demand.
 [IN] : bool : bAutoConnectWhenRoaming : whether the device should auto connect when roaming.
 false = forbidden, true = open.
 [IN] : function : callback(bResult) : call back function, and the parameters list below:
 [IN] : bool : bResult : true = succeed, false = failed.
 return : void
 **************************************************************************/
function SetConnectionMode(strConnectionMode, bAutoConnectWhenRoaming, callback)
{
    connectionMode = strConnectionMode;
    autoConnectWhenRoaming = bAutoConnectWhenRoaming;
    callback(true);
}
    var nCustomAccountType = {
        'strAPN' : "PP.INTERNET",
        'strNumber' : "*99#",
        'strDNS' : "192.168.0.1",
        'strDNS2' : "192.168.1.1",
        'strSecurity'  : "3",
        'strUsername' : "aUserName",
        'strPassword' : ""
    };
/**************************************************************************
 Function : SetCustomAccountType
 Description : set custom account type
 Parameters :
 [IN] : string : strAPN                  : APN content
 [IN] : string : strNumber               : dial-up number
 [IN] : string : strDNS                  : main DNS address
 [IN] : string : strDNS2                 : second DNS address
 [IN] : string : strSecurity             : auth type,
 '0' = 'none';
 '1' = 'PAP';
 '2' = 'CHAP';
 '3' = 'PAP;CHAP', it means that 'PAP' is prefered, and 'CHAP' is in the next place.
 if set strSecurity='PAP;CHAP'; it means that 'PAP' is prefered, and 'CHAP' is in the next place
 [IN] : string : strUsername             : Username for this connection
 [IN] : string : strPassword             : Password for this connection
 [IN] : bool : bResult : true = succeed; false = failed
 return : void
 **************************************************************************/
function SetCustomAccountType(strAPN, strNumber, strDNS, strDNS2, strSecurity, strUsername, strPassword, callback) {
    var result = true;
    //Vendor API call to set custom Account Type using values provided (all strings apart from bAutoConnectWhenRoaming)
    //strAPN - APN Name
    //strNumber - Number to be dialed (if required)
    //strDNS - DNS server to be used
    //strDNS2 - DNS2 server to be used
    //strSecurity - Security settings to be used.
    //strUsername - Username for this connection
    //strPassword - Password for this connection
    //Callback requires a boolean value to indicate success or failure
    nCustomAccountType.strAPN = strAPN;
    nCustomAccountType.strNumber = strNumber;
    nCustomAccountType.strDNS = strDNS;
    nCustomAccountType.strDNS2 = strDNS2;
    nCustomAccountType.strSecurity = strSecurity;
    nCustomAccountType.strUsername = strUsername;
    nCustomAccountType.strPassword = strPassword;
    callback(result);
}

/**************************************************************************
Function : GetCustomAccountType
Description : get custom account type
return CustomAccountType with these attributes:
[OUT] : string : strAPN                  : APN content
[OUT] : string : strNumber               : dial-up number
[OUT] : string : strDNS                  : main DNS address
[OUT] : string : strDNS2                 : second DNS address
[OUT] : string : strSecurity             : auth type,
'0' = 'none';
'1' = 'PAP';
'2' = 'CHAP';
'3' = 'PAP;CHAP', it means that 'PAP' is preferred, and 'CHAP' is in the next place.
if set strSecurity='PAP;CHAP'; it means that 'PAP' is preferred, and 'CHAP' is in the next place
[OUT] : string : strUsername             : Username for this connection
[OUT] : string : strPassword             : Password for this connection

This is a fast synchronous function.
**************************************************************************/
function GetCustomAccountType() {
    // To be populated by Huawei
    return nCustomAccountType;
}


strAccType = "Prepaid";
function SetAccountType(strAccountType, callback)
{
    var result = true; //use this to store the success/failure of the API request and return in the callback
	//if (strAccountType.indexOf('WebSession') !== -1) {
		//result = false;
	//}
    strAccType=strAccountType;
    //Vendor API call to set Account Type to strAccountType (string) specified in ProfileMatchingFields.xls
    //strAccountType is a string value, which indicates the account type. All the values list below:
    //   'Contract', 'Prepaid', 'Business', 'Consumer', 'WebSession'.
    //Callback requires a boolean value to indicate success or failure
    callback(result);
}

// Invokes callback with a semi-colon separated list of valid account types.
function GetAccountType(callback)
{
    var result = true; //use this to store the success/failure of the API request and return in the callback
    var strAccountType = "WebSessions:A;Contract:B;Prepaid:C;Business:D;Consumer:E";
    //Vendor API call to get Account Type specified in ProfileMatchingFields.xls
    //Callback requires a boolean value to indicate success or failure
    callback(strAccountType, result);
}

//strAccountType is a string value, which indicates the account type. All the values list below:
//   'Contract', 'Prepaid', 'Business', 'Consumer', 'WebSession'.
function GetDefaultAccountType()
{
    var result=true;
    //var strAccountType = "Pay as you go:smart";
    return strAccType;
}

// return a string value, which indicates the apn
function GetApn()
{
    return "PP.INTERNET";
}

function SetBearerPreference(strBearerPreference, callback)
{
    //Please map result to a boolean value to indicate success or failure
    var result = true;

    // strBearerPreference contains one of '2G Only', '3G Only', '3G Preferred, '4G Only', '4G Preferred'
    if(typeof strBearerPreference === 'string'){
        bearerPreference = strBearerPreference;
    } else {
        result = false;
    }

    callback(result);
}

function GetBearerPreference(callback)
{
    //Please map strBearerPreference to one of '2G Only', '3G Only', '3G Preferred, '4G Only', '4G Preferred'
    var strBearerPreference = bearerPreference;

    //Please map result to a boolean value to indicate success or failure
    var result = true;

    callback(strBearerPreference, result);
}

/*
var readID = Id of the message read by user;
*/
function SetMessageRead(readID, callback)
{
     var result = true;
    callback(result);
}


function SetMessageCenter(strMessageCenter, callback)
{
    var result = true; //use this to store the success/failure of the API request and return in the callback
    //Vendor API call to set MessageCenter using strMessageCenter (string)


    //Callback requires a boolean value to indicate success or failure
    callback(result);
}

function GetMessageCenter(callback)
{
    var result = true; //use this to store the success/failure of the API request and return in the callback
    var strMessageCenter = 'test 787878';
    //Vendor API call to get MessageCenter as a string and place in the var strMessageCenter

    //Callback requires a boolean value to indicate success or failure
    callback(strMessageCenter, result);
}

function ResetDataCounter(callback)
{
    var result = true; //use this to store the success/failure of the API request and return in the callback
    //Vendor API call to reset DataUsage

    //Callback requires a boolean value to indicate success or failure
    callback(result);
}

function RestoreFactorySettings(callback)
{
    var result = true; //use this to store the success/failure of the API request and return in the callback
    //Vendor API call to restore factory settings of the device.

    //Callback requires a boolean value to indicate success or failure
    callback(result);
}

function RebootDevice()
{
    //Vendor API call to reboot the device.
}

/**************************************************************************
Function : GetRoamingStatus
Description : get Roaming status
Parameters :  
    [IN] : function : callback(bResult, bRoamingStatus) : call back function, and the parameters list below:
        [IN] : bool : bResult         : true = succeed; false = failed.
        [IN] : bool : bRoamingStatus  : roaming status, true = 'Roaming', false = 'no Roaming'.
        if get roaming status failed the bRoamingStatus value will be null. 
return : void
**************************************************************************/
function GetRoamingStatus(callback)
{
	var bRoamingStatus = true;
	callback(true, bRoamingStatus);
}

    /**************************************************************************
Function : GetDateTime
     Description: Get UTC date and time in a specified format. This is typically done by querying an NTP server.
     Parameters :
      [IN] : function : callback(bResult, vDateTime) : call back function with the argument list:
      [IN] : bool : bResult : true = succeed, false = failed
      [IN] : object : vDateTime : JavaScript object, with the attribute list:
     string : dateTime : the UTC date/time returned by an NTP server, formatted as per RFC3339, 5.6. Internet Date/Time Format
      (http://tools.ietf.org/html/rfc3339#section-5.6), for example: 1990-12-31T23:59:60Z
      Return : void

**************************************************************************/
function GetDateTime(callback)
{
	var vDateTime = {dateTime : '2000-02-29T23:59:59Z'};
	callback(true, vDateTime);
}

/**************************************************************************
Function : GetNetworkLocation
 Description: Get network location information from the device, typically by issuing a command from the AT+CREG family.
 Parameters :
  [IN] : function : callback(bResult, vNetworkLocation) : call back function with the argument list:
  [IN] : bool : bResult : true = succeed, false = failed
  [IN] : object : vNetworkLocation: JavaScript object, with the attribute list:
   integer : cellId : Cell Id
   integer : rncId : Radio Network Controller (RNC) Id, (only for 3G networks)
   integer : lac : Location Area Code
   [for example, vNetworkLocation = { cellId: 1, rncId: 2, lac: 3}]
 Return : void

**************************************************************************/
function GetNetworkLocation(callback)
{
	var vNetworkLocation = {cellId: 1, rncId: 2, lac: 3};
	callback(true, vNetworkLocation);
}

/**************************************************************************
Function : GetSimCardStatus
Description : get sim card status
Parameters : void
return: object : vSimCardStatus : the object containing the sim card status info, it's attributes list below,
        if failed, we will return null.
       type : name     : description
	number : nSimState : the sim card state, maybe one of following value:
	         1 = sim card ready;
	         2 = no sim card or invalid sim card;
	         3 = pin required;
	         4 = puk required;
	boolean : bPinState : the pin state, maybe one of following value:
	         true  = pin enable;
	         false = pin disable;
	number : nSimPinTimes : attempt times to enter pin code, it is between 0 and 3,
             if sim card state is not pin-required, the value will be 0.
    number : nSimPukTimes : attempt times to enter puk code, it is between 0 and 10,
             if sim card state is not puk-required, the value will be 0.
comment: when the sim card is ready, it will be one of following situation:
         first: the pin state is enable, but the user has already enter the correct pin code in the beginning.
         second: the pin state is disable.
         so the webUI needs to check the detail situation as above.
**************************************************************************/
function GetSimCardStatus()
{
    var vSimCardStatus = {};
    vSimCardStatus.nSimState = 1;
    vSimCardStatus.bPinState = pinRequired;
    vSimCardStatus.nSimPinTimes = 3;
    vSimCardStatus.nSimPukTimes = 10;

    return vSimCardStatus;
}



function VendorStart()
{
    timer = setInterval(TimerCallbackFunction, 1000);
    //setTimeout(function(){currentNetworkConnectionStatus = "disconnected";},5000);
}

/**************************************************************************
Function    : Supports
Description : reports if a specified feature is supported
Parameters  : string : feature   : one of VENDOR_1_7, VENDORWIFI_1_7, DLNA_1_14, WPS_1_14
Return      : bool   : supported : true if the specified feature is supported, otherwise false
Comment     : This function is intended to future-proof WebUI against future changes to the Vendor and VendorWiFi APIs.
              There is an understanding that all vendors will fully implement these two APIs to at least version 1.7.
              Future additions to the APIs will be added to list of allowed values.
              If a vendor implements the feature, this function will return true.
              In all other cases, this function will return false.
**************************************************************************/
function Supports(feature)
{
    var supported = false;
    switch(feature)
    {
        case 'VENDOR_1_7':
        case 'VENDORWIFI_1_7':
        case 'DLNA_1_14':
        case 'WPS_1_14':
        //case 'LTE_1_16':
            supported = true;
            break;
    }
    return supported;
}

function SetAPICallbackObject(callbackDestination) {
    apiCallbackObject = callbackDestination;
}

function ForceCallbacks() {
    TimerCallbackFunction();
}

$(document).ready(function ()
{
    // Everything inside this will load as soon as the DOM is loaded and before the page contents are loaded.
    VendorStart();
});

return {
    IsDoubleTapEnabled: IsDoubleTapEnabled,
    SetDoubleTapEnabled: SetDoubleTapEnabled,
    GetWebUIProductName: GetWebUIProductName,
    GetPhoneNumber: GetPhoneNumber,
    SavePhoneNumber: SavePhoneNumber,
    GetCurrentNetwork: GetCurrentNetwork,
    GetIMSIFromDevice: GetIMSIFromDevice,
    GetPinTimes: GetPinTimes,
    GetPUKTimes: GetPUKTimes,
    GetProductName: GetProductName,
    GetSoftwareVersion: GetSoftwareVersion,
    GetHWVersion: GetHWVersion,
    GetSerialNumber: GetSerialNumber,
    GetSimSerialNumber: GetSimSerialNumber,
    GetDataCounter: GetDataCounter,
    TimerCallbackFunction: TimerCallbackFunction,
    GetIMEI: GetIMEI,
    IsPINRequired: IsPINRequired,
    GetSIMStatus: GetSIMStatus,
    GetIPAddress: GetIPAddress,
    GetDNS: GetDNS,
    SetLanguage: SetLanguage,
    IsMessagePreview: IsMessagePreview,
    SetMessagePreview: SetMessagePreview,
    GetLanguage: GetLanguage,
    ScanForNetwork: ScanForNetwork,
    SetNetwork: SetNetwork,
    SetNetworkAcquisitionToAutomatic: SetNetworkAcquisitionToAutomatic,
    GetNetworkAcquisitionMode: GetNetworkAcquisitionMode,
    SendSMSAndGetResponse: SendSMSAndGetResponse,
    GetSMSMessages: GetSMSMessages,
    SaveSMSMessage: SaveSMSMessage,
    SetNewSMSCallbackFunction: SetNewSMSCallbackFunction,
    GetSMSCount: GetSMSCount,
    GetSMSStorageCapacityState: GetSMSStorageCapacityState,
    SendSMS: SendSMS,
    DeleteSMS: DeleteSMS,
    DeleteMultipleSMS: DeleteMultipleSMS,
    SendUSSDAndGetResponse: SendUSSDAndGetResponse,
    Connect: Connect,
    Disconnect: Disconnect,
    EnterPin: EnterPin,
    EnablePin: EnablePin,
    DisablePin: DisablePin,
    EnterPUK: EnterPUK,
    GetConnectionMode: GetConnectionMode,
    SetConnectionMode: SetConnectionMode,
    SetCustomAccountType: SetCustomAccountType,
    GetCustomAccountType: GetCustomAccountType,
    SetAccountType: SetAccountType,
    GetAccountType: GetAccountType,
    GetDefaultAccountType: GetDefaultAccountType,
    GetApn: GetApn,
    SetBearerPreference: SetBearerPreference,
    GetBearerPreference: GetBearerPreference,
    SetMessageRead: SetMessageRead,
    SetMessageCenter: SetMessageCenter,
    GetMessageCenter: GetMessageCenter,
    ResetDataCounter: ResetDataCounter,
    RestoreFactorySettings: RestoreFactorySettings,
    RebootDevice: RebootDevice,
    GetRoamingStatus: GetRoamingStatus,
    GetSimCardStatus: GetSimCardStatus,
    GetDateTime: GetDateTime,
    GetNetworkLocation: GetNetworkLocation,
    VendorStart: VendorStart,
    Supports: Supports,
    SetAPICallbackObject: SetAPICallbackObject,
    ForceCallbacks: ForceCallbacks,
    GetSupportedConnectionModes: GetSupportedConnectionModes,
    GetRemainingUnlockNetworkAttempts: GetRemainingUnlockNetworkAttempts,
    UnlockNetwork: UnlockNetwork
    };

}());

$.extend( window, Vendor );

