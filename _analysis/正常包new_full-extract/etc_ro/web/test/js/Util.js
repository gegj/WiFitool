// Util.js
// Place for utility functions and objects that :
//  (1) Stand alone, and can be tested independently.
//  (2) Are used in more than one place.

var Util = (function () {

   /* Removes [object] from Array [array].  Does nothing if not present.
    * The object to remove must be exactly equal to one in the array.
    */
    function removeFromArray(object, array) {
        var i = $.inArray(object, array);
        if (i !== -1) {
            array.splice(i, 1);
        }
    }

   // Removes a collection of objects from an array
    function removeAllFromArray(itemsToRemove, array) {
        $.each(itemsToRemove, function (index, item) {
            removeFromArray(item, array);
        });
    }

    // Make an array of the results of calling func on each element in fromObject
    function collect(fromObject, func) {
        var result = [];
        $.each(fromObject, function (index, item) {
            result.push(func(item));
        });
        return result;
    }

    function isEmptyObject(obj){
        var i;
        for(i in obj){ if(obj.hasOwnProperty(i)){return false;}}
        return true;
    }

    // Answers true if the object is the VendorWifi API error object.
    function isErrorObject( object ) {
        return typeof object.errorType === 'string';
    }

// Answers true if we're in a unit test.
    function inUnitTest() {
        return typeof window.QUnit === 'object';
    }

    var bearerMap = {};
    function initialiseBearerMap() {
        bearerMap = {
            map: [
                    {keys:['GSM', 'GPRS'], value:OpCo.TwoG},
                    {keys:['EDGE'], value:OpCo.edge},
                    {keys:['TD_SCDMA', 'WCDMA'], value:OpCo.ThreeG},
                    {keys:['HSDPA'], value:OpCo.HSDPA},
                    {keys:['HSPA', 'HSUPA'], value:OpCo.HSUPA},
                    {keys:['HSPA+'], value:OpCo.HSPAPLUS},
                    {keys:['EVDO Rev.0', 'EVDO Rev.A', 'EVDO Rev.B'], value:OpCo.LTE}
            ],
            getBearerByKey: function (key){
                var bearer = $.grep(bearerMap.map, function(map){
                    return $.inArray(key, map.keys) !== -1;
                });
                return bearer.length > 0 ? bearer[0].value : '';
            }
        };
    }

    /**
     * Causes hookFunction to be called after each call to functionName, which is a method on ownerObject.
     *
     * @param ownerObject - use window for global functions...
     * @param functionName - string representation of the function
     * @param hookFunction
     */
    function addHookAfter(ownerObject, functionName, hookFunction) {
        var functionReplaced = ownerObject[functionName];
        ownerObject[functionName] = function () {
            var result = functionReplaced.apply(this, arguments);
            hookFunction.apply(this, arguments);
            return result;
        };

        // Unusual idiom.  We use the function object as the place to store information:
        ownerObject[functionName].functionReplaced = functionReplaced;
    }

    function removeHook(ownerObject, functionName) {
        if( $.isFunction( ownerObject[functionName].functionReplaced ) ) {
            ownerObject[functionName] = ownerObject[functionName].functionReplaced;
        }
    }

    function checkTextForWatermark(message) {
        if (message.indexOf( languageManager.ENTER_YOUR_RESPONSE + getWatermark() ) !== -1) {
           return '';
        }
        return message;
    }

    function roundToTwoDecimalPlaces(num) {
        return Math.round(num * 100) / 100;
    }

    function findIndexByKeyinJsonArray(obj, key)
    {
        var i = 0;
        for (i = 0; i < obj.length; i++) {
            if (obj[i][key]) {
                return i;
            }
        }
        return null;
    }


    return {
        addHookAfter : addHookAfter,
        removeHook : removeHook,
        collect: collect,
        getBearerByKey : function(key) { return $.isFunction(bearerMap.getBearerByKey) ? bearerMap.getBearerByKey(key) : '';},
        initialiseBearerMap : initialiseBearerMap,
        removeFromArray: removeFromArray,
        removeAllFromArray: removeAllFromArray,
        inUnitTest: inUnitTest,
        isEmptyObject: isEmptyObject,
        isErrorObject: isErrorObject,
        checkTextForWatermark : checkTextForWatermark,
        roundToTwoDecimalPlaces : roundToTwoDecimalPlaces,
        findIndexByKeyinJsonArray : findIndexByKeyinJsonArray,
        zzz: 0
    };
}());

// DO NOT REMOVE ! This is NOT Comments, it's code
// RG 28/10/2011, sourced from http://ajaxref.com/assets/005/5560.pdf

/*@cc_on @if (@_win32 && @_jscript_version >= 5) if (!window.XMLHttpRequest)
window.XMLHttpRequest = function() { return new ActiveXObject('Microsoft.XMLHTTP') }
@end @*/

