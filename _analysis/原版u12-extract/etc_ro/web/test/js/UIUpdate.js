// Variable holding any URL parameters specified with this page.
// PrePay balance - should be set almost immediately via doBalanceCheck()
// call in account-balance.htm.
var prepayBalance = "";
var networkMccmnc = '23415';

/*
 * This list must contain all the OpCos that we have files for.
 * If you add a new OpCo update this list.
 *
 * The logic of this code is that we can assume that if we have an OpCo
 * number of length 7 it will never clash with a 6 or 5 length one.
 */
var allOpcos = [
    20205,
    20404,
    20601,
    20810,
    21401,
    21670,
    21910,
    22210,
    22601,
    22801,
    23003,
    23201,
    23402,
    23415,
    23801,
    2380171,
    2380172,
    24405,
    24602,
    24705,
    24802,
    26202,
    26801,
    27077,
    27201,
    27402,
    27602,
    27801,
    28001,
    28401,
    28602,
    2860251,
    28802,
    29340,
    29403,
    40004,
    40401,
    40405,
    40411,
    40413,
    40415,
    40420,
    40427,
    40430,
    40443,
    40446,
    40460,
    40484,
    40488,
    40566,
    40567,
    405750,
    405751,
    405752,
    405753,
    405754,
    405755,
    405756,
    41302,
    42403,
    42602,
    42702,
    45406,
    46692,
    50213,
    50219,
    50503, 50506, // Note that we are re-using 50503 for 50506 data for now
    52503,
    53001,
    54201,
    5420171,
    60202,
    62002,
    63001,
    64004,
    64304,
    64710,
    65101,
    65501,
    73001
];

function getAndSetXMLData(xmlURL, elementId) {
    $.get(xmlURL, function(data) {
        $('#' + elementId).html(data);
    });
}
function setUrlParams(parameterString) {
    var e,
        a = /\+/g,  // Regex for replacing addition symbol with a space
        r = /([^&=]+)=?([^&]*)/g,
        d = function (s) {
            return decodeURIComponent(s.replace(a, " "));
        },
        q = parameterString;

    urlParams = {};

    for (e = r.exec(q); e; e = r.exec(q)) {
        urlParams[d(e[1])] = d(e[2]);
    }
}

/*
 * This function will run at load time and read any URL parameters into urlParams.
 */
setUrlParams(window.location.search.substring(1));

if(typeof urlParams.deviceName === 'string' && (urlParams.deviceName === "R210" || urlParams.deviceName === "R205" || urlParams.deviceName ==="R203-Z")){
     productDeviceName = urlParams.deviceName;
}
function getWatermark() {
    return ' \n\n ';
}

//utility functions
function showThrobber() {
    $('#throbber').show();
}

function hideOverlay() {
    $(".overlay").hide();
    $(".genericPopupOverlay").hide();
    $(".whiteBg").hide();
}

function hideThrobber() {
    $('#throbber').hide();
    hideOverlay();
}

function setLoadPageVisible() {
    $('#loadPage').css('display', 'block');
    $('#fullPage').show();
    hideThrobber();
}

function getTitle() {
    return typeof PageLoader === 'object' && PageLoader.isMobileWifi() ?
        TagSubstitution.DEVICE_NAME() : languageManager.TITLE_QUICKSTART;
}

function setTitle() {
    document.title = getTitle();
}

function resizeLoginForm() {
    var minWidth = 0;
    var elem = $('#loginForm');
    if (elem.width() < minWidth) {
        elem.width(minWidth);
    }
}

function getCurrentPageName() {
    return typeof PageLoader === 'object' ? PageLoader.getCurrentPageName() + '.htm' : window.location.href;
}

function loadPage(pageName, pageParameters) {
    if (typeof PageLoader === 'object') {
        PageLoader.loadPage(pageName, pageParameters);
    } else {
        window.location.href = pageName + (pageName.indexOf('.htm') ? '' : '.htm') + (pageParameters ? '?' + pageParameters : '');
    }
    return false;
}

/*
 * Function to write to console log - if any.
 */
function writeToConsole(message) {
    if (typeof console === 'object' && typeof console.log === 'function') {
        console.log(message);
    }
}

/*
 * Function to log messages
 */
function logMessage(message) {
    writeToConsole(message);
}

function getPhoneNumberFromForm(strSMSNumber) {
    var strSMSNum = strSMSNumber !== languageManager.WRITE2_RECIPIENT_NUMBER ? strSMSNumber.replace(/\s/g,''):strSMSNumber;
    if(strSMSNum===languageManager.WRITE2_RECIPIENT_NUMBER){//if user does not enter a number then ignore default text.
        strSMSNum="";
    }
    return strSMSNum;
}
function getMessageFromForm(strSMSMessage) {
    if($.trim(strSMSMessage) === languageManager.ENTER_YOUR_RESPONSE){//Dont save boiler plate text as message
        strSMSMessage='';
    }
    strSMSMessage = $.trim(strSMSMessage); //trim spaces
    strSMSMessage = strSMSMessage.length > 0 ? strSMSMessage : ' '; // ensure a minimum string for hyperlinking later
    return strSMSMessage;
}

function saveSMSBeforeRedirect()
{
    // Need not validate on mandatory number for save as draft.
    var number = PageLoader.getCurrentPageName()==="sms-write2"?jQuery('#smsRecipient').val():urlParams.num;
    var message = jQuery('#smsReply').val();
    if (number.length > 0 && number !== languageManager.WRITE2_RECIPIENT_NUMBER) {
        if (!UserNotification.formValidated()) {return;}
    }
    /* if number.length === 0 and (msg.length === 1 and msg === ' ' ) then
     do not save
     */
    if(getPhoneNumberFromForm(number).length === 0 && (getMessageFromForm(message).length === 1 && getMessageFromForm(message) === ' ')){
        loadPage('sms-redirect');
    }
    else{
        // "-1" means a new message to save
        saveSMSMessage(message,number, -1,SMS.redirectSaveDraftCallback);
    }
}

$(document).ready(function () {
    if (Util.inUnitTest()) {
        return;
    }
});

//this function includes all necessary js files for the application
function include(file) {

    var script = document.createElement('script');
    script.src = file;
    script.type = 'text/javascript';
    script.defer = true;

    document.getElementsByTagName('head').item(0).appendChild(script);

}

function setTextElement(element, text) {
    var substitutedText = text;
    try {
        if (typeof TagSubstitution === 'object') {
            substitutedText = TagSubstitution.substituteText(text);
        }

        if (element.tagName.toLowerCase() === "input" && element.getAttribute("type").toLowerCase() === "text") {
            $(element).val(substitutedText);
        }
        else {
            $(element).html(substitutedText);
        }
    }
    catch(err) {
        element.value = substitutedText;
    }
}

function setTextItem(id, text) {
    var elem = document.getElementById(id);
    if (elem) {
        setTextElement(elem, text);
    }
}

function addOptionToCombo(ComboId, optionName, optionValue) {
    var elem = document.getElementById(ComboId);
    var option = document.createElement('option');
    option.value = optionValue;
    option.innerHTML = optionName;
    elem.appendChild(option);
}

function enterPUKErrorState() {
    return GetPUKTimes() === 0 ? 'lock' : 'error';
}

function enterPUKLoadState() {
    return GetPUKTimes() === 0 ? 'lock' : 'ok';
}

function enterPUKDisplayState(state) {
    if (state === 'lock') {
        setTextItem("pukRemainingAttempts1", languageManager.PUK_NO_MORE_ATTEMPTS);
        setTextItem("pukRemainingAttempts2", languageManager.PUK_NO_MORE_ATTEMPTS);
        $("#PukIncorrect").show();
        $("#PukIncorrectOthers").hide();
    }
    else
    if (state === 'error') {
        setTextItem("pukRemainingAttempts1", languageManager.REMAINING_PUK_ATTEMPTS_1);
        setTextItem("pukRemainingAttempts2", languageManager.REMAINING_PUK_ATTEMPTS_2);
        setTextItem("pukRemainingAttempts3", languageManager.REMAINING_PUK_ATTEMPTS_3);
        $("#pinNoMatch, #pinNoMatchParagraph, #pukEnterAttempts").hide();//hide no pin match text
        $("#PukIncorrect, #pukRemainingAttempts2").show();
        $("#pukEnterLabel, #puk").removeClass().addClass("error");
    }
    else {
        $("#fPukIncorrect, #pukRemainingAttempts2").hide();
    }
    $("#frmPukRequired").show();
}

function displayPinStatus(pinValue) {
    if (pinValue.length > 0) {
        setTextItem('simPinStatusText', languageManager.QUICKSTARTPIN_STATUS);
        $("#simPinStatusText").removeClass().addClass("greenTick");
    }
    else {
        setTextItem('simPinStatusText', languageManager.SIM_AMEND_PINSTATUS);
        $("#simPinStatusText").removeClass().addClass("redCross");
    }
}

/* Amend the pin-stored page display according to whether PIN Stored is set. */
function displayStoredPinStatus(pinStoredValue) {
    if (pinStoredValue.length > 0) {
        setTextItem('simPinParagraph1', languageManager.NOPIN_PARA1_STORED);
        jQuery("#rememberPin").attr('checked', true);
    }
    else {
        setTextItem('simPinParagraph1', languageManager.NOPIN_PARA1);
        jQuery("#rememberPin").attr('checked', false);
    }
}

function toggleOffListOfNetworks() {
    $('#returnedNetworks').html('');
}

var networkData = {
    strFullName : '',
    strShortName : '',
    strNumeric : '',
    nRat : -1,
    nState : -1,
    nMode : -1,
    strBearer: ''
};

function getEmptyNetworkDataObject() {
    networkData.connect = undefined;
    networkData.registering = undefined;
    return $.extend(networkData, {
        strFullName : '',
        strShortName : '',
        strNumeric : '',
        nRat : -1,
        nState : -1,
        nMode : -1,
        strBearer: ''
    });
}

function resetTextItemForRecharge() {
    var topUpName = 'TopUp';
    if (OpCo) {
        topUpName = OpCo.top_up_name === "" ? "TopUp" : OpCo.top_up_name;
    }
    setTextItem('mainNavAccountTopUp', '<a data-pageName="top-up">' + '<span>' + topUpName + '</span></a>');
    $('#mainNavAccountTopUp').attachLinkOnClickHandler();
}

function addOption(id, selectbox, text, value) {
    var selectedItem = GetDefaultAccountType() === value;
    var optn = document.createElement("OPTION");
    optn.text = text;
    optn.value = value;
    optn.id = id;
    optn.selected = selectedItem;
    selectbox.options.add(optn);
}

function isNumeric(sText) {
    var i = 0;
    var ValidChars = "0123456789.";
    var IsNumber = true;
    var Char;
    for (i = 0; i < sText.length && IsNumber === true; i++) {
        Char = sText.charAt(i);
        if (ValidChars.indexOf(Char) === -1) {
            IsNumber = false;
        }
    }
    return IsNumber;
}

function formatPrepayBalance(response) {
    prepayBalance = response;
    if (imsi.indexOf('23415') !== -1) {
        var replaceSrc = OpCo.balance_check_replace === '' ? '#' : OpCo.balance_check_replace;
        var replaceDst = OpCo.balance_check_with === '' ? '&pound;' : OpCo.balance_check_with;
        prepayBalance = response.replace(replaceSrc, replaceDst);
    }
    return prepayBalance;
}

function roundNumber(number, decimals) { // Arguments: number to round, number of decimal places
    var newnumber = number.toFixed(parseInt(decimals, 10));
    return parseFloat(newnumber); // Output the result to the form field (change for your purposes)
}

function formatTopUpBalance(response) {
    topupBalance = response;
    if (imsi.indexOf('23415') !== -1) {
        topupBalance = response.replace("#", " £");
    }
    return topupBalance;
}

function pukRequired() {
    var result = false;
    var attemptNumber = GetPinTimes();
    if (attemptNumber <= 0) {
        result = true;
    }
    return result;
}

function setConnectionModeCallback(result) {
    var connectionMode = $("#connectionMode").val();
    if (!result) {
        logMessage("setConnectionModeCallback failed");
    }
    else {
        setAutoConnectionText(connectionMode);
    }
}

function setConnectionSettingsCallBack(result) {
    UserNotification.showGenericVendorResponse(result);
    if (!result) {
        logMessage("setConnectionSettingsCallBack failed");
    }
}

function attachLoadPageListener() {
    $('body').attachLinkOnClickHandler();
}

function setIPAddress(strIPAddress) {
    setTextItem("IPAddress", strIPAddress);
}

function setDNS2() {
    var customAccountType = GetCustomAccountType();
    if(customAccountType !== null){
        setTextItem("dns2", customAccountType.strDNS2);
    }
}

function setDNS(DNS) {
    setTextItem("dns", DNS);
    setDNS2();
}

////////////////////////////////////////

// Potential input strings:
// connected
// disconnected
// Requested (See Mantis issue 0027657):
// searchingForNetwork
// connecting
var setQuickStartStatusConnection = $.noop; // TODO : Not required

//UIUpdate functions
function SetBannerIcon(strPathToBannerIcon) {
    var elem = document.getElementById('partnerLogo');

    if (elem && strPathToBannerIcon.length > 0) {
        elem.innerHTML = '<img src=\"OpCo/' + strPathToBannerIcon + '\" alt=\"\" />';
    }
}

function SetSupportURL(strSupportURL) {
    $('#footerLinkSupport').attr('href', strSupportURL);
}

function SetHomePageURL(strHomePageUrl) {
    var elem = document.getElementById('mainNavVodafoneLogo');

    if (elem) {
        elem.innerHTML = '<a href=\"' + strHomePageUrl + '\" title=\"Back to home page\"><img src=\"img/menu/logoSml.gif\" alt=\"Vodafone logo - home page\" /></a>';
    }
}

//QuickStart
//SMS
//Account
function setOuterSelectedTab(outerTab) {
    $('#toNavQuickStart, #topNavStorage, #topNavSMS, #topNavAccount').removeClass('current');
    switch (outerTab) {
        case 'QuickStart':
            $('#toNavQuickStart').addClass('current');
            break;

        case 'Storage':
            $('#topNavStorage').addClass('current');
            break;

        case 'SMS':
            $('#topNavSMS').addClass('current');
            break;

        case 'Account':
            $('#topNavAccount').addClass('current');
            break;
    }
}

function setInnerSelectedTab(tab) {
    $('#mainNavAccountBalance, #mainNavAccountTopUp, #mainNavAccountDetails, #mainNavAccountType, #mainNavAccountHelp, ' +
        '#mainNavQuickStartSettings, #mainNavQuickStartHelp, #mainNavSmsDraft, #mainNavSmsInbox, #mainNavSmsSent,' +
        '#mainNavSmsSettings, #mainNavSmsHelp, .filesharing, .filestorage, #mainNavSmsWrite, #mainNavQuickStartWifi,' +
        '#mainNavQuickStartRouter').removeClass('current');
    switch (tab) {
        case 'AccountBalance':
            $('#mainNavAccountBalance').addClass('accountBalance current');
            break;

        case 'AccountTopUp':
            $('#mainNavAccountTopUp').addClass('topUp current');
            break;

        case 'AccountDetails':
            $('#mainNavAccountDetails').addClass('accountDetails current');
            break;

        case 'AccountType':
            $('#mainNavAccountType').addClass('accountType current');
            break;

        case 'AccountHelp':
            $('#mainNavAccountHelp').addClass('help current');
            break;

        case 'Settings':
            $('#mainNavQuickStartSettings').addClass('settings current');
            break;

        case 'Help':
            $('#mainNavQuickStartHelp').addClass('help current');
            break;

        case 'Draft':
            $('#mainNavSmsDraft').addClass('settings current');
            break;

        case 'Inbox':
            $('#mainNavSmsInbox').addClass('settings current');
            break;

        case 'Sent':
            $('#mainNavSmsSent').addClass('settings current');
            break;

        case 'SmsSettings':
            $('#mainNavSmsSettings').addClass('settings current');
            break;

        case 'SMSHelp':
            $('#mainNavSmsHelp').addClass('settings current');
            break;

        case 'StorageFileSharing':
            $('.filesharing').addClass('current');
            break;

        case 'StorageFileStorage':
            $('.filestorage').addClass('current');
            break;

        case 'Write':
            $('#mainNavSmsWrite').addClass('settings current');
            break;

        case 'WiFi':
            $('#mainNavQuickStartWifi').addClass('current');
            break;

        case 'Router':
            $('#mainNavQuickStartRouter').addClass('current');
            break;
    }
}

function getDataRateDisplayString(dataRateInBytesPerSecond) {

    var numberOfBitsInOneBitPerS = 1;
    var numberOfBitsInOneKBitsPerS = numberOfBitsInOneBitPerS * 1024;
    var numberOfBitsInOneMBitsPerS = numberOfBitsInOneKBitsPerS * 1024;
    var numberOfBitsInOneGBitsPerS = numberOfBitsInOneMBitsPerS * 1024;

    var labelForOneBitPerS = 'b/s';
    var labelForOneKBitsPerS = 'Kb/s';
    var labelForOneMBitsPerS = 'Mb/s';
    var labelForOneGBitsPerS = 'Gb/s';

    if (languageManager) {
        labelForOneBitPerS = languageManager.BITSPERSECOND;
        labelForOneKBitsPerS = languageManager.KILOBITSPERSECOND;
        labelForOneMBitsPerS = languageManager.MEGABITSPERSECOND;
        labelForOneGBitsPerS = languageManager.GIGABITSPERSECOND;
    }

    var dataRateInBitsPerSecond = dataRateInBytesPerSecond * 8;

    var vol = dataRateInBitsPerSecond / numberOfBitsInOneGBitsPerS;
    var displayString = Util.roundToTwoDecimalPlaces(vol) + labelForOneGBitsPerS;

    if (vol < 0.5) {
        vol = dataRateInBitsPerSecond / numberOfBitsInOneMBitsPerS;
        displayString = Util.roundToTwoDecimalPlaces(vol) + labelForOneMBitsPerS;

        if (vol < 0.5) {
            vol = dataRateInBitsPerSecond / numberOfBitsInOneKBitsPerS;
            displayString = Util.roundToTwoDecimalPlaces(vol) + labelForOneKBitsPerS;

            if (vol < 0.5) {
                vol = dataRateInBitsPerSecond;
                displayString = Util.roundToTwoDecimalPlaces(vol) + labelForOneBitPerS;
            }
        }
    }

    return displayString;
}

function setDataUpAndDown(dataUp, dataDown) {
    intDataDown = isNaN(dataDown) ? 0 : parseInt(dataDown, 10);
    intDataUp = isNaN(dataUp) ? 0 : parseInt(dataUp, 10);

    setTextItem('mobileDataRateTextUp', getDataRateDisplayString(intDataUp));
    setTextItem('mobileDataRateTextDown', getDataRateDisplayString(intDataDown));
    setTextItem('dataRateText', getDataRateDisplayString(intDataDown + intDataUp));
}

function ReplaceText(id, textToReplace, ReplacementText) {
    var elem = document.getElementById(id);
    if (elem) {
        var str = elem.innerHTML;
        str = str.replace(textToReplace, ReplacementText);
        elem.innerHTML = str;
    }
}

function setNetworkName() {
    var networkName = 'You are connected to %%NETWORK_NAME%%. Your connection is set to %%CONNECTION_TYPE%%. If you are having connection problems you can change this setting. You can also search for additional networks although roaming charges may apply.';
    var networkNameDisconnected = 'You are not connected to %%NETWORK_NAME%%. Your connection is set to %%CONNECTION_TYPE%%. If you are having connection problems you can change this setting. You can also search for additional networks although roaming charges may apply.';
    if (languageManager) {
        networkName = languageManager.NETWORK_SETTINGS_PARA1;
        networkNameDisconnected = languageManager.NETWORK_SETTINGS_PARA1_1;
    }

    var networkText = typeof networkData.registering === 'string' ?
            networkData.registering : ((networkData.strFullName !== '' ? networkData.strFullName : networkData.strNumeric) + ' ' + Util.getBearerByKey(networkData.strBearer));// networkData.strBearermaybe one of :HSUPA, HSDPA ...

    setTextItem('mobileNetworkText', networkText);
    setTextItem('networkSettingsParagraph1', ConnectionManager.isConnected() ? networkName : networkNameDisconnected);
}

function getTotalVolumeDisplayString(totalVolume) {

    var numberOfBytesInOneB = 1;
    var numberOfBytesInOneKB = numberOfBytesInOneB * 1024;
    var numberOfBytesInOneMB = numberOfBytesInOneKB * 1024;
    var numberOfBytesInOneGB = numberOfBytesInOneMB * 1024;

    var labelForOneB = 'B';
    var labelForOneKB = 'KB';
    var labelForOneMB = 'MB';
    var labelForOneGB = 'GB';

    if (languageManager) {
        labelForOneB = languageManager.BYTE;
        labelForOneKB = languageManager.KILOBYTE;
        labelForOneMB = languageManager.MEGABYTE;
        labelForOneGB = languageManager.GIGABYTE;
    }

    var vol = totalVolume / numberOfBytesInOneGB;
    var displayString = Util.roundToTwoDecimalPlaces(vol) + labelForOneGB;

    if (vol < 0.5) {
        vol = totalVolume / numberOfBytesInOneMB;
        displayString = Util.roundToTwoDecimalPlaces(vol) + labelForOneMB;

        if (vol < 0.5) {
            vol = totalVolume / numberOfBytesInOneKB;
            displayString = Util.roundToTwoDecimalPlaces(vol) + labelForOneKB;

            if (vol < 0.5) {
                vol = totalVolume;
                displayString = Util.roundToTwoDecimalPlaces(vol) + labelForOneB;
            }
        }
    }

    return displayString;
}

function setTotalVolume(totalVolume) {
    var displayString = getTotalVolumeDisplayString(totalVolume);
    setTextItem('mobileDataVolumeText', displayString);
    setTextItem('totalVolumeText', displayString);
}

function ConvertTime() {
    ConvertTime.prototype.secondsToDayString =
        function secondsToString(seconds) {
            var time = "0 secs";
            if (!seconds || seconds === "") {
                seconds = 0;
            }
            var numdays = Math.floor(seconds / 86400);
            var numhours = Math.floor((seconds % 86400) / 3600);
            var numminutes = Math.floor(((seconds % 86400) % 3600) / 60);
            var numseconds = ((seconds % 86400) % 3600) % 60;
            if (numdays === 0 && numhours === 0 && numminutes === 0 && numseconds === 0) {
                time = "0 secs";
            }
            else {
                var daysText = 'days';
                var hoursText = 'hours';
                var minutesText = 'mins';
                var secondsText = 'secs';

                if (!Util.isEmptyObject(languageManager)) {
                    daysText = languageManager.DAYS;
                    hoursText = languageManager.HOURS;
                    minutesText = languageManager.MINUTES;
                    secondsText = languageManager.SECONDS;
                }

                var days = (numdays === 0) ? "" : " " + numdays + " " + daysText;
                var hours = (numhours === 0) ? "" : " " + numhours + " " + hoursText;
                var minutes = (numminutes === 0) ? "" : " " + numminutes + " " + minutesText;
                var secs = (numseconds === 0) ? "" : " " + numseconds + " " + secondsText;
                time = days + hours + minutes + secs;
            }
            return jQuery.trim(time);
        };
    ConvertTime.prototype.secondsToHHMMSS =
        function secondsToHHMMSS(seconds) {
            var date = new Date((seconds || 0) * 1000);

            var h = '0' + date.getUTCHours();
            var m = '0' + date.getUTCMinutes();
            var s = '0' + date.getUTCSeconds();

            var hh = h.substring(h.length - 2);
            var mm = m.substring(m.length - 2);
            var ss = s.substring(s.length - 2);

            return hh + ':' + mm + ':' + ss;
        };
}

function setTotalTime(strTime) {
    var convertSeconds = new ConvertTime();
    setTextItem('totalTimeText', convertSeconds.secondsToDayString(strTime));// hooked up to ConvertTime() above.
}

function PromptForDataCounterReset() {

}

function setTimeConnected(timeConnectedStr) {
    var convertSeconds = new ConvertTime();
    setTextItem('mobileConnectTimeText', convertSeconds.secondsToHHMMSS(timeConnectedStr));// hooked up to ConvertTime() above.
    setTextItem('upTimeText', convertSeconds.secondsToDayString(timeConnectedStr));
}

function setTimeSinceStartUp(strTime) {
    var timeString = new ConvertTime();
    setTextItem('timeSinceStartUp', timeString.secondsToDayString(strTime));
}

function setSignalStrength(bars) {
    // To test these pages locally use this
   // bars = '0';
    if (bars !== '0' && window.location.href.indexOf('?') !== -1 && (PageLoader.getCurrentPageName() === 'sim-error' || PageLoader.getCurrentPageName() === 'pin-required' || PageLoader.getCurrentPageName() === 'puk-required')) {
        window.location.replace('launch.html');
    }
    $('#mobileSignaltext').attr('class', function () {
        var className = 'signalStrength';
        switch (bars) {
            case '1':
                className += 'One';
                break;
            case '2':
                className += 'Two';
                break;
            case '3':
                className += 'Three';
                break;
            case '4':
                className += 'Four';
                break;
            case '5':
                className += 'Five';
                break;
            default:
                className += 'None';
                break;
        }
        return className;
    });
}

//end networkStatus functions

//diagnostics.htm update functions

function setProductName(strProductName) {
    setTextItem('productName', strProductName);
}

function getWebUIVersion() {
    return WebUIVersion;
}

function setWebUIVersion(strWebUIVersion) {
    setTextItem('webUIVersion', strWebUIVersion);
}

function setSoftwareVersion(strSoftwareVersion) {
    setTextItem('softwareVersion', strSoftwareVersion);
}

function setHardwareVersion(strHardwareVersion) {
    setTextItem('hardwareVersion', strHardwareVersion);
}

function setSerialNumber(strSerialNumber) {
    setTextItem('serialNumber', strSerialNumber);
}

function setSIMSerialNumber(strSIMSerialNumber) {
    setTextItem('simSerialNumber', strSIMSerialNumber);
}
function setSimNumber(strSimNumber) {
    setTextItem('phoneNumber', strSimNumber);
}

function setIMEI(strIMEI) {
    setTextItem('IMEI', strIMEI);
}

function setIMSI(strIMSI) {
    setTextItem('IMSI', strIMSI);
}

function setSIMStatus(strSIMStatus) {
    var simStatus = "";
    switch (strSIMStatus) {
        case 1:
            simStatus = languageManager.TR_READY;
            break;
        case 2:
            simStatus = languageManager.PIN_REQUIRED;
            break;
        case 3:
            simStatus = languageManager.PUK_IS_REQUIRED;
            break;
        case 4:
            simStatus = languageManager.TR_NO_SIM_OR_INVALID_CARD;
            break;
        default:
            simStatus = "";
            break;
    }
    setTextItem('simStatus', simStatus);
}

//Account-Balance.htm update functions

//language functions

function checkFileExists(fileName) {
    var result = false;
        $.ajax({
            type: "HEAD",
            async: false,
            url: fileName,
            complete: function(response, textStatus){
                result = response.status === 200;
            }
        });
    return result;
}

function checkLanguageFileExists(languageFileName) {
    return checkFileExists('Language/' + languageFileName);
}

/*
 * Return the file corresponding to a language code.
 * Defaults to "en-gb.js".
 * E.g. "Spanish" returns "es-es.js"
 *
 */
function selectLanguageFileSrc(lang) {
    var langFileName = (lang + ".js").toLowerCase();
    return checkLanguageFileExists(langFileName) ? langFileName : 'en-gb.js';
}

function callPostLanguageFunctions() {
    /*Can be used to replace Quickstart with Mobile Wi-Fi as both are never translated
     if(PageLoader.isMobileWifi()){
     $('#loadPage').replaceText('QuickStart','Mobile Wi-Fi',OpCo.operator,[]);
     }*/
    attachLoadPageListener();
    if (!PageLoader.isQuickStart() && typeof showOrHideLoggedInItems === 'function' /*&& !isLoggedIn()*/) {
        showOrHideLoggedInItems(false);
    }
    /*else if (typeof showOrHideLoggedInItems === 'function' && !PageLoader.isQuickStart()) {
        showOrHideLoggedInItems(true);
    }*/
    else {
        $('.loggedIn').show();
        $('.notLoggedIn').hide();
    }

    $("#smsInboxIdArea").addClass('');

    if (OpCo && OpCo.account_type === 'Prepaid') {

        if (OpCo && OpCo.prepay.check === '') {
            $('#mainNavAccountBalance').hide();
        }

        else {
            $('#mainNavAccountBalance').show();
        }

        if (OpCo && OpCo.prepay.topup === '' && OpCo.prepay.online === "") {
            $('#mainNavAccountTopUp').hide();
        }

        else if (OpCo && OpCo.prepay.topup === '') {
            $('#mainNavAccountTopUp').show();
            $('#voucherTopUp').hide();
        }

        else if (OpCo && OpCo.prepay.online === '') {
            $('#mainNavAccountTopUp').show();
            $('#onlineTopUp').hide();
        }

        else {
            $('#mainNavAccountTopUp').show();
        }
    }
    else {
        $('#mainNavAccountBalance').hide();
        $('#mainNavAccountTopUp').hide();
    }

}

function isDutch() {
    if(OpCo){
        return $.inArray(OpCo.country, ["Netherlands"]) > -1 ? true : false;
    }
    return false;
}

function isGhanaian() {
    if(OpCo){
        return $.inArray(OpCo.country, ["Ghana"]) > -1 ? true : false;
    }
    return false;
}

function isIrish() {
    if(OpCo){
        return $.inArray(OpCo.country, ["Ireland"]) > -1 ? true : false;
    }
    return false;
}

function isSouthAfrican() {
    if(OpCo){
        return $.inArray(OpCo.country, ["South Africa"]) > -1 ? true : false;
    }
    return false;
}

function isSpanish() {
    if(OpCo){
        return $.inArray(OpCo.country, ["Spain"]) > -1 ? true : false;
    }
    return false;
}

function isTurkish() {
    if(OpCo){
        return $.inArray(OpCo.country, ["Turkey", "Turkey (Cyprus)"]) > -1 ? true : false;
    }
    return false;
}

function substituteMccMncSpecificValuesIntoLanguageManager() {
    // for all languageManager keys having a corresponding _OpCo-specific value, use the latter
    // e.g. TOPUP will be overloaded with TOPUP_MCCMNC_23415
    var mccmnc = parseInt(networkMccmnc, 10);
    $.each(languageManager, function(languageManagerKey, languageManagerVal) {
        var regexp = new RegExp('(.*)_MCCMNC_' + mccmnc + '$', 'g');
        var mccmncLanguageManagerKeyMatches = regexp.exec(languageManagerKey);
        if (mccmncLanguageManagerKeyMatches) {
            var mccmncLanguageManagerKey = mccmncLanguageManagerKeyMatches[1];
            languageManager[mccmncLanguageManagerKey] = languageManagerVal;
        }
    });
    if($.inArray(mccmnc, ['50503','50506']) > -1 ) {
        $('.australia').show();
    }
}

/*
 * Note:
 * This is called at the end of each language javascript files (such as en-gb.js).
 */
function TranslatePage() {
    if (("undefined" !== typeof(appLanguage)) && ("null" !== typeof(appLanguage))) {
        languageManager = appLanguage;

        substituteMccMncSpecificValuesIntoLanguageManager();


        if (typeof PageLoader === 'object' && PageLoader.isMobileWifi()) {
            setTextItem('topNavQuickStartContainer', '<a data-pageName="welcome" id="toNavQuickStart"><span>' + languageManager.TITLE_MOBILEWIFI + '</span></a>');
        } else {
            setTextItem('topNavQuickStartContainer', '<a data-pageName="welcome" id="toNavQuickStart"><span>' + languageManager.QUICKSTART + '</span></a>');
        }

        setTextItem('chooseLanguageTitle', languageManager.CHOOSE_LANGUAGE);
        setTextItem('mainNavAccountBalance', '<a data-pageName="account-balance">' + '<span>' + languageManager.AB_BALANCE + '</span>' + '</a>');
        attachLoadPageListener();
        var topUpName = 'TopUp';
        if (OpCo) {
            topUpName = OpCo.top_up_name === "" ? "TopUp" : OpCo.top_up_name;
            setTextItem('accountDetailsRegisterBtn', TagSubstitution.OPCO_ACCOUNT_REGISTRATION_URL());
        }
        setTextItem('mainNavAccountTopUp', '<a data-pageName="top-up">' + '<span>' + topUpName + '</span></a>');
        setTextItem('mainNavAccountDetails', '<a data-pageName="account-details">' + '<span>' + languageManager.AD_ACCOUNT_DETAILS + '</span></a>');
        setTextItem('mainNavAccountType', '<a data-pageName="account-type">' + '<span>' + languageManager.AD_ACCOUNT_TYPE + '</span></a>');
        setTextItem('mainNavAccountHelp', '<a data-pageName="help" data-pageParameters="context=Account">' + '<span>' + languageManager.HELP + '</span></a>');

        setTextItem('accountBalanceTitle', languageManager.BALANCE_TITLE);
        setTextItem('accountBalanceSubHeading', '<span>' + languageManager.ACCOUNT_BALANCE + '</span>');
        setTextItem('accountBalanceRefreshBtn', '<span>' + languageManager.REFRESH + '</span>');
        setTextItem('mobileNumberLabel', languageManager.MOBILE_NUMBER);
        setTextItem('mobileSignalLabel', languageManager.SIGNAL);
        setTextItem('mobileStatusLabel', languageManager.STATUS);
        setTextItem('mobileNetworkLabel', languageManager.NETWORK);
        setTextItem('mobileConnectTimeLabel', languageManager.TIME_CONNECTED);
        setTextItem('mobileDataVolumeLabel', languageManager.TOTAL_VOL);
        $('#mobileDataVolumeLabel').attr('title', languageManager.TR_VIEW_USAGE_DETAILS);
        setTextItem('mobileDataRateUpLabel', '<span>' + languageManager.UP + '</span>');
        setTextItem('mobileDataRateDownLabel', '<span>' + languageManager.DOWN + '</span>');
        setTextItem('mobileDataRateTextUp', '<span>' + languageManager.UP_TEXT + '</span>');
        setTextItem('mobileDataRateTextDown', '<span>' + languageManager.DOWN_TEXT + '</span>');

        setTextItem('accountDetailsTitle', languageManager.AD_ACCOUNT_DETAILS);
        setTextItem('accountDetailsSubheading', languageManager.AD_ACCOUNT_DETAILS);
        setTextItem('accountDetailsParagraph1', languageManager.AD_ACCOUNT_DETAILS_1);
        setTextItem('accountDetailsParagraph2', languageManager.AD_ACCOUNT_DETAILS_2);
        setTextItem('accountHelpTitle', languageManager.NUMBER_HELP_1);
        setTextItem('accountHelpYourNumber', languageManager.NUMBER_HELP);
        setTextItem('helpBoxTwoTitle', languageManager.TOPUP_HELP);
        setTextItem('accountTypeTitle', languageManager.AD_ACCOUNT_TYPE);
        setTextItem('accountTypeLabel', '<strong>' + languageManager.AD_ACCOUNT_TYPE + '</strong>');
        setTextItem('accountTypeParagraph1', languageManager.ACCOUNT_TYPE_TEXT_TYPE_1);
        setTextItem('accountTypeParagraph2', languageManager.ACCOUNT_TYPE_TEXT_TYPE_2);
        setTextItem('accountTypeParagraph3', languageManager.ACCOUNT_TYPE_TEXT_TYPE_3);

        if (typeof PageLoader === 'object' && PageLoader.isMobileWifi()) {
            setTextItem('helpDiagnosticsCol3Paragraph1', languageManager.CONNECTING_HELP_TEXT2);
            setTextItem('connectingHelpParagraph2', languageManager.CONNECTING_HELP_TEXT2);
            setTextItem('simErrorHelpCol3Paragraph1', languageManager.SIM_ERROR_COL31);
        } else {
            setTextItem('helpDiagnosticsCol3Paragraph1', languageManager.CONNECTING_HELP_TEXT21);//same text in 3 id's
            setTextItem('connectingHelpParagraph2', languageManager.CONNECTING_HELP_TEXT21);
            setTextItem('simErrorHelpCol3Paragraph1', languageManager.CONNECTING_HELP_TEXT21);
            setTextItem('mainNavQuickStartSettings', '<a data-pageName="network-status">' + '<span>' + languageManager.SETTINGS + '</span>' + '</a>');
            setTextItem('mainNavQuickStartHelp', '<a data-pageName="help" data-pageParameters="page=help">' + '<span>' + languageManager.HELP_CONNECTING + '</span>' + '</a>');
        }

        setTextItem('mainNavSmsInbox', '<a data-pageName="sms-inbox"><span>' + languageManager.INBOX + '</span></a>');
        setTextItem('mainNavSmsWrite', '<a data-pageName="sms-write2" data-pageParameters="num=&message="><span>' + languageManager.INBOX_WRITE + '</span></a>');
        setTextItem('mainNavSmsSent', '<a data-pageName="sms-sent"><span>' + languageManager.INBOX_SENT + '</span></a>');
        setTextItem('mainNavSmsDraft', '<a data-pageName="sms-draft"><span>' + languageManager.INBOX_DRAFT + '</span></a>');
        setTextItem('mainNavSmsSettings', '<a data-pageName="sms-settings"><span>' + languageManager.INBOX_SETTINGS + '</span></a>');
        setTextItem('mainNavSmsHelp', '<a data-pageName="help" data-pageParameters="context=SMS"><span>' + languageManager.INBOX_HELP + '</span></a>');
        setTextItem('leftNavQuickStartStatus', '<a data-pageName="network-status">' + languageManager.STATUS + '</a>');
        setTextItem('leftNavQuickStartSimPin', '<a data-pageName="pin-stored">' + languageManager.SIM_PIN + '</a>');
        setTextItem('leftNavQuickStartConnection', '<a data-pageName="connection">' + languageManager.NETWORK_SETTINGS_CONNECTION + '</a>');
        setTextItem('leftNavQuickStartNetwork', '<a data-pageName="network-settings">' + languageManager.NETWORK + '</a>');
        setTextItem('connectionSettingsTitle', languageManager.MOBILE_BROADBAND_CONNECTION_SETTINGS);
        setTextItem('connectionSettingsStatusText', languageManager.NOT_CONNECTED);
        setTextItem('connectionSettingsAccountTypeLabel', languageManager.ACCOUNT_TYPE_CONNECTION + '<span class="mandatory">' + '*' + '</span>');
        setTextItem('connectionSettingsAPNLabel', languageManager.APN + '&nbsp;<span>' + '*' + '</span>');
        setTextItem('connectionSettingsConnectNumLabel', languageManager.NUMBER + '&nbsp;<span>' + '*' + '</span>');
        setTextItem('connectionSettingsDNSLabel', languageManager.DNS);
        setTextItem('connectionSettingsDNS2Label', languageManager.DNS2);
        setTextItem('connectionSettingsSecurityLabel', languageManager.SECURITY);
        setTextItem('PAP', languageManager.PAP);
        setTextItem('CHAP', languageManager.CHAP);
        setTextItem('PAP_CHAP', languageManager.PAP_CHAP);
        setTextItem('connectionSettingsUsernameLabel', languageManager.USERNAME);
        setTextItem('connectionSettingsPasswordLabel', languageManager.PASSWORD);
        setTextItem('connectionSettingsPasswordLabel2', languageManager.CONFIRM_PASSWORD);
        setTextItem('connectionSettingsIdleTimeLabel', languageManager.IDLE_TIME);
        setTextItem('connectionSettingsConnectModeLabel', languageManager.CONNECTION_MODE);
        setTextItem('connectionSettingsConnectModeSelect1', languageManager.AUTOMATIC);
        setTextItem('connectionSettingsConnectModeSelect2', languageManager.PROMPT);
        setTextItem('allowGuestUsersCheckboxLabel', languageManager.allowGuestUsersCheckboxLabel);
        setTextItem('allowGuestMobileSettingsCheckboxLabel', languageManager.allowGuestMobileSettingsCheckboxLabel);
        setTextItem('connectionSettingsSaveBtn', '<span>' + languageManager.SAVE + '</span>');
        setTextItem('connectionSettingsHelpTitle', languageManager.CONNECTING_HELP);
        setTextItem('connectionSettingsHelpCol11', languageManager.CONNECTION_HELP_COL11_1);
        setTextItem('connectionSettingsHelpCol12', languageManager.CONNECTION_HELP_COL11_2);
        setTextItem('connectionSettingsHelpCol2Title1', languageManager.CONTRACT_SETTINGS);
        setTextItem('connectionSettingsHelpCol2Paragraph1', languageManager.CONNECTION2_HELP_COL21);
        setTextItem('connectionSettingsHelpCol2Title2', languageManager.PREPAY);
        setTextItem('connectionSettingsHelpCol2Paragraph2', languageManager.SETTINGS_HELP_COL22);
        setTextItem('connectSettingsHelpCol2Title3', languageManager.WEB_SESSIONS_CONNECTION);
        setTextItem('connectSettingsHelpCol2Paragraph3', languageManager.SETTINGS_HELP_COL23);
        setTextItem('connectionSettingsHelpCol3Title1', languageManager.OTHER_CONNNECTION2);
        setTextItem('connectionSettingsHelpCol3Title1Paragraph1', languageManager.CONNECTION2_HELP_COL31);
        setTextItem('connectionSettingsHelpCol3Title2', languageManager.CONNECTION_MODE);
        setTextItem('connectionSettingsHelpCol3Title2Paragraph1', languageManager.CONNECTION2_HELP_COL32_1);
        setTextItem('connectionSettingsHelpCol3Title2Paragraph2', languageManager.CONNECTION2_HELP_COL32_2);
        setTextItem('defaultPageTitle', languageManager.INFO_HEADING);
        setTextItem('defaultPageParagraph1', languageManager.EXPLANATORY);
        setTextItem('defaultPageMessagesTitle', languageManager.MESSAGES_DEFAULT);
        setTextItem('defaultPageTblTitleDate', languageManager.DATE_DEFAULT);
        setTextItem('defaultPageTblTitleTime', languageManager.DATE_FROM);
        setTextItem('defaultPageTblTitleMessages', languageManager.DATE_MESSAGES);
        setTextItem('defaultpageWarningText', languageManager.WARNING);
        setTextItem('defaultPageHelpTitle', languageManager.CONTEXTUAL_HELP);
        setTextItem('defaultPageHelpCol1', languageManager.COL1);
        setTextItem('defaultPageHelpCol2', languageManager.COL2);
        setTextItem('defaultPageHelpCol3', languageManager.COL3);
        setTextItem('leftNavQuickStartHelp', '<a data-pageName=\"help\">' + languageManager.DIAGNOSTICS_HELP_LEFT + '</a>');
        setTextItem('leftNavHelpDiagnostics', '<a data-pageName="diagnostics">' + languageManager.DIAGNOSTICS + '</a>');
        setTextItem('leftNavHelpDeviceControls', '<a data-pageName="device-controls">' + languageManager.DEVICE_CONTROLS + '</a>');
        setTextItem('helpDiagnosticsTitle', (PageLoader.isMobileWifi() ? languageManager.VODAFONE_MOBILE_WIFI_DIAGNOSTICS : languageManager.VODAFONE_QUICKSTART_DIAGNOSTICS));
        setTextItem('helpDiagnosticsAccordionTitle1', '<a onclick="toggleVisibility(\'helpDiagnosticsAccordianContent1\');" class="arrowBtn cursorPointer">' + '</a>' + '<a onclick="toggleVisibility(\'helpDiagnosticsAccordianContent1\');" class="cursorPointer">' + (PageLoader.isMobileWifi() ? languageManager.VODAFONE_MOBILE_WIFI_INFO : languageManager.VODAFONE_QUICKSTART_INFO) + '</a>');
        setTextItem('helpDiagnosticsAccordionTitle2', '<a onclick="toggleVisibility(\'helpDiagnosticsAccordianContent2\');" class="arrowBtn cursorPointer">' + '</a>' + '<a onclick="toggleVisibility(\'helpDiagnosticsAccordianContent2\');" class="cursorPointer">' + languageManager.MOBILE_BROADBAND_CONN_STATUS + '</a>');
        setTextItem('helpDiagnosticsRefreshBtn', '<span>' + languageManager.REFRESH + '</span>');
        setTextItem('helpDiagnosticsHelpTitle', languageManager.DISGNOSTICS_HELP);
        setTextItem('deviceControlsWarningMessage', languageManager.WARNING_MESSAGE);
        setTextItem('deviceControlsEmulationLabel', '<strong>' + languageManager.DEVICE_EMULATION + '</strong>');
        setTextItem('deviceEmulation', languageManager.RDNIS);
        setTextItem('errorDetectionPopupTitle', languageManager.CONNECTION_DETECTION);
        setTextItem('errorDetectionParagraph1', languageManager.ERROR_DETECTION_PARA1);
        setTextItem('errorConnectionOkBtn', '<span>' + languageManager.ERROR_OK + '</span>');
        setTextItem('genericErrorTitle', languageManager.ERROR_HEADING);
        setTextItem('genericErrorParagraph1', languageManager.SUPPORTING_TEXT);
        setTextItem('genericErrorHelpTitle', languageManager.CONTEXTUAL_HELP);

        if (PageLoader.isMobileWifi()) {
            setTextItem('indexPageTitle', languageManager.WELCOME_TO_VODAFONE_MOBILE_WIFI);
            setTextItem('indexPageParagraph1', languageManager.INDEX_PARA_1_MOBILE_WIFI);
            setTextItem('indexPageParagraph2', languageManager.INDEX_PARA_2_MOBILE_WIFI);
            setTextItem('indexPageHelpTitle', languageManager.INDEX_MOBILE_WIFI_HELP_TITLE);
        } else {
            setTextItem('indexPageTitle', languageManager.WELCOME_TO_VODAFONE_QUICKSTART);
            setTextItem('indexPageParagraph1', languageManager.INDEX_PARA_1);
            setTextItem('indexPageParagraph2', languageManager.INDEX_PARA_2);
            setTextItem('indexPageHelpTitle', languageManager.INDEX_HELP_TITLE);
        }

        setTextItem('smsMessageTableTitle', '<span>' + languageManager.MESSAGES_ONE + '</span>');
        setTextItem('smsTableTitleDate', languageManager.INDEX_DATE);
        setTextItem('smsTableTitleFrom', languageManager.INDEX_FROM);
        setTextItem('smsTableTitleMESSAGE', languageManager.INDEX_MESSAGES);
        setTextItem('indexPageHelpCol2Paragraph1', '<strong>' + languageManager.INDEX_HELP_COL21 + '</strong>');
        setTextItem('networkSettingsTitle', languageManager.NETWORK_SETTINGS_TITLE);
        setTextItem('networkSettingsParagraph1', languageManager.NETWORK_SETTINGS_PARA1);
        setTextItem('networkSettingsSubHeading', languageManager.NETWORK_NAME);
        setTextItem('networkSettingsConnectionTypeLabel', languageManager.NETWORK_SETTINGS_CONNECTION_TYPE);
        setTextItem('networkSettings3gPrefLabel', languageManager.THREEG_PREFERRED);
        setTextItem('networkSettings3gLabel', languageManager.THREEG);
        setTextItem('networkSettingsGPRSLabel', languageManager.GPRS_ONLY);
        setTextItem('networkSettingsSaveBtn', '<span>' + languageManager.NETWORK_SETTINGS_SAVE + '</span>');
        setTextItem('networkSettingsPreferredLabel', languageManager.NETWORK_SETTINGS_PREFERRED);
//        setTextItem('preferredNetworkRadio1Text', languageManager.VODAFONENZ_3G_CURRENT);
        setTextItem('preferredNetworkRadio2Text', languageManager.TELECOM_GPRS);
        setTextItem('preferredNetworkRadio3Text', languageManager.TWODEGREES);
        setTextItem('preferredNetworkRadio4Text', languageManager.TELSTRACLEAR);
        setTextItem('preferredNetworkRadio5Text', languageManager.BLACK_WHITE_3G);
        setTextItem('networkSettingsSaveBtn2', languageManager.NETWORK_SETTINGS_SAVED);
        setTextItem('networkSettingsHelpTitle', languageManager.NETWORK_SETTINGS_HELP);
        setTextItem('networkSettingsHelpCol1Paragraph1', languageManager.NETWORK_SETTINGS_HELP_COL11_1);
        setTextItem('networkSettingsHelpCol1Paragraph2', languageManager.NETWORK_SETTINGS_HELP_COL11_2);
        setTextItem('networkSettingsHelpCol2Paragraph1','<strong>'+ languageManager.NETWORK_SETTINGS_HELP_COL21 + '</strong>');
        setTextItem('networkSettingsHelpCol2Paragraph2', languageManager.NETWORK_HELP_COL22);
        setTextItem('networkSettingsHelpCol3Paragraph1', languageManager.NETWORK_SETTINGS_HELP_COL31);
        setTextItem('networkSettingsPreferredLabel', languageManager.NETWORK_REFRESH_PREFERRED);
        setTextItem('networkType', languageManager.NETWORK_SETTINGS_PREFERRED);
//        setTextItem('preferredNetworkRadio1Text', languageManager.NETWORK_REFRESH_CURRENT);
        setTextItem('preferredNetworkRadio2Text', languageManager.TELECOMGPRS_FORBIDDEN);
        setTextItem('preferredNetworkRadio3Text', languageManager.TWODEGREES3G_FORBIDDEN);
        setTextItem('preferredNetworkRadio4Text', languageManager.TESTRA_FORBIDDEN);
        setTextItem('preferredNetworkRadio5Text', languageManager.BLACK_WHITE3G_FORBIDDEN);
        setTextItem('networkSettingsRefreshBtn', '<span>' + languageManager._SEARCH + '</span>');
        setTextItem('networkSettingsCancelBtn', '<span>' + languageManager.NETWORK_REFRESH_CANCEL + '</span>');
        setTextItem('networkSettingsSaveBtn2', '<span>' + languageManager.NETWORK_REFRESH_SAVE + '</span>');
        setTextItem('networkSettingsMessage', languageManager.NETWORK_SETTINGS_MESSAGE);
        setTextItem('notConnectedTitle', languageManager.NOT_LOGGED_NOTCONNECTED);
        setTextItem('notConnectedParagraph1', languageManager.NOTCONNECTED_PARA1_1);
        setTextItem('notConnectedParagraph2', languageManager.NOTCONNECTED_PARA1_2);
        setTextItem('notConnectedConnectBtn', '<span>' + languageManager.NOTCONNECTED_BTN + '</span>');
        setTextItem('notConnectedHelpTitle', languageManager.NOTCONNECTED_HELP);
        setTextItem('notConnectedHelpCol1Paragraph1', languageManager.NOT_CONNECTED_HELP_COL11);
        setTextItem('notConnectedHelpCol1SubParagraph1', languageManager.NOT_CONNECTED_HELP_COL111);
        setTextItem('notConnectedHelpCol2Paragraph1', languageManager.NOT_CONNECTED_HELP_COL21);
        setTextItem('simPinSettingsTitle', languageManager.NOPIN_SIM_SETTINGS);
        setTextItem('simPinParagraph1', languageManager.NOPIN_PARA1);
        setTextItem('simPinQuickStartParagraph1', languageManager.NOPIN_QUICKSTART_PARA1);
        setTextItem('simPinChange1Label', languageManager.Enter_PIN_to_change);
        setTextItem('simPinChange2Label', languageManager.Enter_new_PIN);
        setTextItem('simPinChange3Label', languageManager.Confirm_new_PIN);
        setTextItem('pinCodeRequiredCheckBox', languageManager.PIN_code_required);
        setTextItem('simPinStoreLabel', languageManager.Store_my_PIN);
        setTextItem('simPinHelpTitle', languageManager.SIM_PIN_Help);
        setTextItem('pinRequiredTitle', languageManager.PIN_REQUIRED);
        setTextItem('pinRequiredParagraph1', languageManager.ENTER_PIN_PARA);
        setTextItem('pinRequiredQuickStartParagraph1', languageManager.ENTER_QUICKSTART_PIN_PARA);
        setTextItem('simPinChangeAttempts', languageManager.PIN_CHANGE_ATTEMPTS);
        setTextItem('pinRequiredSubmitBtn', '<span>' + languageManager.PIN_SUBMIT + '</span>');
        setTextItem('pinRequiredHelpTitle', languageManager.SIM_PIN_HELP);
        setTextItem('pinRequiredHelpCol1Paragraph1', languageManager.PIN_HELP_COL11);
        setTextItem('pinRequiredHelpCol2Paragraph1', '<strong>' + languageManager.PIN_HELP_COL21_1 + '</strong>');
        setTextItem('pinRequiredHelpCol2Paragraph2', languageManager.PIN_HELP_COL21_2);
        setTextItem('pinRequiredHelpCol3Paragraph1', languageManager.PIN_HELP_COL31);
        setTextItem('pinIncorrectParagraph', languageManager.PIN_INCORRECT_PARA);
        setTextItem('pukEnterAttempts', languageManager.ATTEMPT1_of10);
        setTextItem('simPinChange1Label', languageManager.PUK_ENTER_NEW_PIN);
        setTextItem('pukRequiredCol1Paragraph1', languageManager.PUK_COL11);
        setTextItem("pukRemainingAttempts1", languageManager.REMAINING_PUK_ATTEMPTS_1);
        setTextItem("pukRemainingAttempts2", languageManager.REMAINING_PUK_ATTEMPTS_2);
        setTextItem("pukRemainingAttempts3", languageManager.REMAINING_PUK_ATTEMPTS_3);
        setTextItem('quickStartStatusPin', languageManager.QUICKSTART_DISCONNECT_PIN_STATUS);
        setTextItem('quickStartStatusCol1Paragraph1', languageManager.QUICKSTART_DISCONNECT_COL11);
        setTextItem('simErrorTitle', languageManager.SIM_ERROR_NOTDETECTED);
        setTextItem('simErrorHelpTitle', languageManager.SIM_ERROR_HELPTITLE);
        setTextItem('simErrorParagraph1', languageManager.SIM_ERROR_PARA1);
        setTextItem('simErrorHelpCol1Paragraph1', languageManager.SIM_ERROR_COL11);
        setTextItem('simErrorHelpCol2Paragraph1', languageManager.SIM_ERROR_COL21);
        setTextItem('simPinSettingsParagraph1', languageManager.SIM_AMEND_PARA1);
        setTextItem('simPinChange1Label', languageManager.SIM_AMEND_CHANGE);
        setTextItem('simPinChangeSuccessful', '<strong>' + languageManager.SIM_AMEND_SUCCESSFULL + '</strong><br/>' + languageManager.SIM_AMEND_SUCCESSFULL1);
        setTextItem('simPinSettingsHelpTitle', languageManager.SIM_AMEND_PINHELP);
        setTextItem('simPinSettingsTitle', languageManager.INCORRECT_PINSETTINGS);
        setTextItem('simPinSettingsParagraph1', languageManager.SIM_AMEND_PARA1);
        setTextItem('simPinChangeUnSuccessful', '<strong>' + languageManager.INCORRECT_UNSUCCESSFUL + '</strong>');
        setTextItem('simPinChangeUnSuccessful2', languageManager.INCORRECT_UNSUCCESSFUL1);
        setTextItem('smsDraftTitle', languageManager.DRAFT_TITLE);
        setTextItem('smsTableTitleDate', languageManager.DRAFT_DATE);
        setTextItem('smsTableTitleTo', languageManager.DRAFT_TO);
        setTextItem('smsTableTitleMessage', languageManager.DRAFT_MESSAGES);
        setTextItem('smsDeleteBtn', '<span>' + languageManager.DRAFT_DELETE + '</span>');
        if (getCurrentPageName().indexOf('sms-inbox') !== -1 && SMS.smsCount) {
            setTextItem('paginationAll', languageManager.DRAFT_SHOWALL);
        }
        setTextItem('smsDraftHelpTitle', languageManager.DRAFT_HELPTITLE);
        setTextItem('smsDraftHelpCol1Paragraph1', languageManager.DRAFT_COL11);
        setTextItem('smsDraftHelpCol2Paragraph1', languageManager.DRAFT_COL21);
        setTextItem('confirmInboxDeletePopupTitle', languageManager.INBOX_DELETE_CONFIRM);
        setTextItem('confirmInboxDeletePopupParagraph1', languageManager.INBOX_DELETE_SMS);
        setTextItem('ConfirmInboxDeletePopupYesBtn', '<span>' + languageManager.INBOX_DELETE_YES + '</span>');
        setTextItem('ConfirmInboxDeletePopupNoBtn', '<span>' + languageManager.INBOX_DELETE_NO + '</span>');
        setTextItem('smsTableTitleDate', languageManager.INBOX_DATE);
        setTextItem('smsTableTitleFrom', languageManager.INBOX_FROM);
        setTextItem('smsTableTitleMessage', languageManager.INBOX_MESSAGES);
        setTextItem('smsForwardBtn', '<span>' + languageManager.INBOX_FORWARD + '</span>');
        setTextItem('warningInboxFull', languageManager.INBOX_WARNING);
        setTextItem('smsInboxHelpTitle', languageManager.INBOX_HELPTITLE);
        setTextItem('smsInboxCol1Paragraph1', languageManager.INBOX_COL11_1);
        setTextItem('smsInboxCol1Paragraph2', languageManager.INBOX_COL11_2);
        setTextItem('smsInboxCol2Paragraph1', languageManager.INBOX_COL21_1);
        setTextItem('smsInboxCol2Paragraph2', languageManager.INBOX_COL21_2);
        setTextItem('smsSentTitle', languageManager.SENT_TITLE);
        setTextItem('smsSentHelpTitle', languageManager.SENT_SMSHELP);
        setTextItem('smsSentHelpHelpCol1Paragraph1', languageManager.SENT_HELP_COL11);
        setTextItem('smsSentHelpHelpCol2Paragraph1', languageManager.INBOX_COL21_1);
        setTextItem('smsSettingsTitle', languageManager.SETTINGS_SMSSETTINGS);
        setTextItem('smsSettingsParagraph1', languageManager.SETTINGS_PARA1);
        setTextItem('smsSettingsMessagePreviewLabel', languageManager.MESSAGE_PREVIEW);
        setTextItem('messagePreviewRadio1Label', languageManager.SETTINGS_ON);
        setTextItem('messagePreviewRadio2Label', languageManager.SETTINGS_OFF);
        setTextItem('smsSettingsParagraph2', languageManager.SETTINGS_PARA2);
        setTextItem('smsSettingsMessageCentreLabel', languageManager.MESSAGE_CENTRE_NUMBER);
        setTextItem('smsSettingsDefaultBtn', '<span>' + languageManager.SETTINGS_DEFAULT + '</span>');
        setTextItem('smsSettingsSaveBtn', '<span>' + languageManager.SETTINGS_SAVE + '</span>');
        setTextItem('smsSettingsHelpTitle', languageManager.SETTINGS_SMSSETTINGS_HELP);
        setTextItem('smsSettingsHelpCol1Paragraph1', languageManager.SETTINGS_HELP_COL11);
        setTextItem('smsSettingsHelpCol2Paragraph1', languageManager.SETTINGS_HELP_COL21);
        setTextItem('smsWriteTitle', languageManager.TITLE_AND_NUMBER_1);
        setTextItem('smsWrite2Title', languageManager.TITLE_AND_NUMBER2_1);
        setTextItem('smsReadTitle', languageManager.READ);
        setTextItem('smsWriteYourNumber', languageManager.TITLE_AND_NUMBER_2);
        setTextItem('smsFromMessageLabel', languageManager.WRITE_MESSAGE);
        setTextItem('smsReplyLabel', languageManager.WRITE_REPLY);
        setTextItem('smsWrite2Label', languageManager.WRITE2_LABEL);
        setTextItem('smsReply', languageManager.ENTER_YOUR_RESPONSE + getWatermark());
        setTextItem('characterCount', languageManager.CHARACTERANDSMS_COUNT2_1);
        setTextItem('smsCount', languageManager.CHARACTERANDSMS_COUNT2_2);
        setTextItem('smsSettingsCloseBtn', '<span>' + languageManager.WRITE_CLOSE + '</span>');
        setTextItem('smsSettingsDraftBtn', '<span>' + languageManager.SAVE_AS_DRAFT + '</span>');
        setTextItem('smsSettingsReplyBtn', '<span>' + languageManager.WRITE_REPLY + '</span>');
        setTextItem('smsWriteHelpTitle', languageManager.WRITE_SMSHELP);
        setTextItem('smsWriteHelpCol1Paragraph1', languageManager.WRITE_HELP_COL11);
        setTextItem('smsWriteHelpCol1Paragraph2', languageManager.WRITE_HELP_COL12);
        setTextItem('smsWrite2HelpCol1Paragraph1', languageManager.WRITE2_HELP_COL11);
        setTextItem('smsWriteHelpCol2Paragraph1', languageManager.WRITE_HELP_COL21);
        setTextItem('smsWrite2HelpCol2Paragraph1', languageManager.WRITE2_HELP_COL21);
        setTextItem('smsRecipientLabel', languageManager.WRITE2_RECIPIENT);
        setTextItem('smsRecipient', languageManager.WRITE2_RECIPIENT_NUMBER);
        setTextItem('smsSendBtn', '<span>' + languageManager.WRITE_SEND + '</span>');
        setTextItem('smsRedirectParagraph1', languageManager.TR_YOU_HAVE_BEEN_REDIRECTED_TO);
        setTextItem('accountTopupThankyou', languageManager.THANK_YOU);
        setTextItem('accountTopupThankyou', languageManager.TOPUP2_BTN_TEXT);
        setTextItem('accountTopupUnsuccessful', languageManager.TOPUP3_UNSUCCESSFUL);
        setTextItem('accountTopupUnsuccessful', languageManager.REENTER_CODE);
        setTextItem('updatesTitle', languageManager.UPDATES);
        setTextItem('updatesParagraph1', languageManager.UPDATE_FREQUENCY);
        setTextItem('updatesFrequencyLabel', languageManager.PLEASE_SELECT);
        setTextItem('updatesFrequency', '<option class="grey" value="">' + languageManager.WEEKLY + '</option>' + '<option class="grey" value="">' + 'Monthly' + '</option>' + '<option class="grey" value="">' + 'Yearly' + '</option>' + '<option class="grey" value="">' + 'Never' + '</option>');
        setTextItem('updatesHelpTitle', languageManager.UPDATES_HELP);

        setTextItem('accountBalanceParagraph1', languageManager.TITLE_AND_NUMBER_2);
        setTextItem('accountBalanceParagraph2', prepayBalance);

        setTextItem('connectionSettingsAccountTypeContract', languageManager.CONNECTION_ACCOUNT_TYPE_CONTRACT);
        setTextItem('connectionSettingsAccountTypePrepaid', languageManager.CONNECTION_ACCOUNT_TYPE_PREPAID);
        setTextItem('connectionSettingsAccountTypeBusiness', languageManager.CONNECTION_ACCOUNT_TYPE_BUSINESS);
        setTextItem('connectionSettingsAccountTypeConsumer', languageManager.CONNECTION_ACCOUNT_TYPE_CONSUMER);
        setTextItem('connectionSettingsAccountTypeWebSession', languageManager.CONNECTION_ACCOUNT_TYPE_WEBSESSION);
        setTextItem('connectionSettingsAccountTypeCustom', languageManager.CUSTOM);

        setTextItem('autoConnectLabel', languageManager.AUTO_CONNECT);

        setTextItem('saveButtonText', languageManager.SAVE);
        setTextItem('helpLinkText', languageManager.HELP_DEFAULT);
        setTextItem('diagnosticsLinkText', languageManager.DIAGNOSTICS);
        setTextItem('deviceControlsLinkText', languageManager.DEVICE_CONTROLS);
        setTextItem('aboutLinkText', languageManager.ABOUT);
        setTextItem('supportLinkText', languageManager.SUPPORT);
        setTextItem('supportIssueSectionTitle', languageManager.SUPPORT);
        setTextItem('supportLinkText', languageManager.SUPPORT);
        setTextItem('aboutWebUIProductName', (PageLoader.isMobileWifi() ? languageManager.ABOUT_MOBILE_WIFI_PRODUCT_NAME : languageManager.ABOUT_PRODUCT_NAME));
        setTextItem('aboutProductManufacturer', languageManager.ABOUT_PRODUCT_MANUFACTURER);
        setTextItem('aboutCopyright', languageManager.ABOUT_COPYRIGHT);
        setTextItem('smsErrorNotSentTitle', languageManager.SMS_NOT_SENT);
        setTextItem('smsErrorNotSentText', languageManager.SMS_NOT_SENT_TEXT);
        setTextItem('smsErrorTextLengthTitle', languageManager.SMS_ERROR_TEXT_TITLE);
        setTextItem('smsErrorTextLengthText', languageManager.SMS_ERROR_TEXT);
        setTextItem('pukBlockedTitle', languageManager.PUK_Blocked);
        setTextItem('dhcpEnabledLabel', languageManager.dhcpEnabledLabel);
        setTextItem('dmzEnabledLabel', languageManager.dmzEnabledLabel);
        setTextItem('dmzEnabled', "%%DMZ_ENABLED%%");
        setTextItem('dhcpEnabled', "%%DHCP_ENABLED%%");

        setTextItem('wiFiStatusLabel', languageManager.wiFiStatusLabel);
        setTextItem('SSIDLabel', languageManager.SSIDLabel);
        setTextItem('modeLabel', languageManager.modeLabel);
        setTextItem('channelLabel', languageManager.channelLabel);
        setTextItem('securitylabel', languageManager.securitylabel);
        setTextItem('helpDiagnosticsAccordionTitle3Label', languageManager.device_CONFIG);
        setTextItem('helpDiagnosticsAccordianContent5Label', languageManager.helpDiagnosticsAccordianContent5);
        setTextItem('diagnosticsTitleIpAddress', languageManager.TR_IP_ADDRESS);
        setTextItem('diagnosticsTitleHostname', languageManager.diagnosticsTitleHostname);
        setTextItem('diagnosticsTitleMacAddress', languageManager.TR_MAC_ADDRESS);
        setTextItem('diagnosticsTitleTime', languageManager.diagnosticsTitleTime);

        if (typeof(doPageSpecificStuff) === 'function') {
            doPageSpecificStuff();
            substituteMccMncSpecificValuesIntoLanguageManager();
        }

        UserNotification.initialiseFieldLengthsForPage();

        callPostLanguageFunctions();

        setTitle();
        if (typeof PageLoader === 'object') {
            PageLoader.translatePageUsingNewArchitecture();
            PageLoader.stylePageUsingNewArchitecture();
        }

        resizeLoginForm();
        setLoadPageVisible();
    }
}
//end language functions

///Script loading

function disableAnchor(obj, disable) {
    if (obj) {
        if (disable) {
            var onclick = obj.getAttribute("onclick");
            if (onclick && onclick !== "" && onclick !== null) {
                obj.setAttribute('href_bak', onclick);
            }
            obj.removeAttribute('href');
            obj.setAttribute('onclick', "");
            $(obj).addClass('disabled');
        }
        else {
            var href_bak = obj.getAttribute("href_bak");
            if (href_bak && href_bak !== "" && href_bak !== null) {
                obj.setAttribute('onclick', obj.attributes.href_bak.nodeValue);
                $(obj).removeClass('disabled');
            }
        }
    } else {
        writeToConsole("disableAnchor was passed an invalid object");
    }
}

function getDataCounterCallback(result, vDataCounter) {
    setDataUpAndDown(vDataCounter.nCurrentUploadRate, vDataCounter.nCurrentDownloadRate); // Param1 is an integer containing uplink speed in b/s, Param2 is an integer containing downlink speed in b/s
    setTimeConnected(vDataCounter.nCurrentConnectTime); //param is string containing the total time connected
    setTimeSinceStartUp(vDataCounter.nTotalLifeTime); //param is string containing total time since device was powered up
    setTotalTime(vDataCounter.nTotalConnectTime); //param is string containing total time
    setTotalVolume(vDataCounter.nTotalUpload + vDataCounter.nTotalDownload); //Param is string containing the total amount of data

}
