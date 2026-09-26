// ***************************************************************************************************
// Extra support for testing.
// ***************************************************************************************************


// ****************** Specials to support jsTestDriver ********************
// The QUnitAdapter for jsTestDriver doesn't provide for async tests.  So stub them out under jsTestDriver.
if (typeof window.asyncTest !== 'function') { // We're jsTestDriver, not in proper QUnit
    window.asyncTest = $.noop;
    window.notEqual = function ( a, b, text ) { return ok(a !== b, text); };
}
// We use equal where we mean strictEqual
window.equal = window.strictEqual;
// ****************** End of jsTestDriver support ********************



function checkMatches(regex, str, comment) {
    comment = comment || '';
    ok(regex.test(str), comment + ': ' + regex + ' matches: ' + str);
}


// Validates that an object matches the given pattern (exemplar).
//   The exemplar has the same structure as the object it's testing, but instead of field values, it usually has
//   functions to check the value.
// Where it has values instead , they are just compared directly.
//    See 'Test validateObject testing function' below for an example.

function validateObject(object, exemplar, comment) {
    function countElements(obj) {
        var i = 0;
        $.each(obj, function () {
            i++;
        });
        return i;
    }

    comment = comment || '';
    equals(countElements(object), countElements(exemplar), comment + ': number of elements');
    $.each(object, function (key, value) {
        if (typeof exemplar[key] === 'undefined') {
            ok(false, comment + ': unexpected key: ' + key);
        } else {
            var exemplarItem = exemplar[key];
            if ($.isFunction(exemplarItem)) {
                ok(exemplarItem(value), comment + ': ' + key);
            }
            else if ($.isArray(exemplarItem)) {
                ok($.isArray(value), comment + ': not array: ' + key);
                $.each(exemplarItem, function (index, nestedExemplarItem) {
                    validateObject(value[index], nestedExemplarItem, ': ' + key + '[' + index + ']');
                });
            }
            else switch (typeof exemplarItem) {
                    case 'boolean':
                    case 'number':
                    case 'string':
                        equals(exemplar[key], value, comment + ': ' + key);
                        break;

                    default:
                        ok(false, comment + ': Bad exemplar: ' + key);
                }
            }
        });
}

// ***************************************************************************************************
// Utility functions to support validateObject
// ***************************************************************************************************


function isBoolean( value ) {
    return typeof value === 'boolean';
}

function isDontCare() {
    return true;
}

function isIntegerString( value ) {
    return typeof value === 'string' && /^[0-9]+$/.test( value );
}

function isIntegerOrNullString( value ) {
    return isIntegerString( value ) || value === '';
}

function isString( value ) {
    return typeof value === 'string' ;
}

function isChn(str){
    var reg = /^[\u0391-\uFFE5]+$/;
    var reh = /[a-zA-Z]+/;
    if(reg.test(str) || reh.test(str)){
     return true;
    }
    return false;
}





function isFullString( value ) {
    return isString( value ) && value.length > 0;
}

function isArrayofObjects(item){
    if ( item === undefined || item === null || item.length === 0 ) {
        return false;
    }
    return $.isArray(item) && typeof item[0] === 'object';
}

function isArrayOfStrings(item) {
    // Could be more rigorous, but this will do:
    return $.isArray(item) && typeof item[0] === 'string';
}

function isInteger(item) {
    return typeof item === 'number' && Math.floor(item) === item;
}

function isNumber(item) {
    return typeof item === 'number';
}

function isLatitude(item) {
    return isNumber(item) && Math.abs(item) <= 90;
}

function isLongitude(item) {
    return isNumber(item) && Math.abs(item) <= 180;
}

function validateDateAndTime(year, month, day, hours, minutes, seconds) {
    var dateObj = new Date(year, month-1, day, hours, minutes, seconds); //We decrement month because it is zero indexed (0 is January)

    if(Object.prototype.toString.call(dateObj) === "[object Date]") {
       if(!isNaN(dateObj.getTime()) && dateObj.getDate() === +day && dateObj.getMonth() === month-1) {
           return true;
       }
    }

    return false;
}

function isInternetDateTime(item) {  //The date will be in the format of '2011-06-31T22:59:59Z'
    var strDate = item.slice(0,10);
    var strTime = item.substring(11, item.length);

    var timeAndZoneRegex = /[0-9\-]{10}[t,T][0-9:]{8}[z,Z]/;
    if ( !timeAndZoneRegex.test(item) ) {
        return false;
    }

    var dateBits = strDate.split('-');
    var timeBits = strTime.split(':');

    return validateDateAndTime(dateBits[0], dateBits[1], dateBits[2], timeBits[0], timeBits[1], timeBits[2].substring(0, timeBits[2].length-1));
}

// Answers a function that verifies a parameter against a list of values, passed as its arguments.
// e.g. f = makeEnumerationChecker( '1', '2' );
// f('1') === true;
// The list may also be passed as an array, as the first argument

function makeEnumerationChecker( first /* ... */ ) {
    var arrayOfValues = $.isArray(first) ? first : $.makeArray( arguments );
    return function( value ) {
        return $.inArray( value, arrayOfValues ) !== -1;
    };
}

function isValidEncryptionType(value) {
    var result = true;
    $.each(value, function (index, indexedValue) {
        if ($.inArray( indexedValue, ['WEP', 'AES', 'TKIP', 'MIX' ] ) === -1) {
            result = false;
        }
    });
    return result;
}

function makeValidEncryptionTypesChecker() {
    return function( value ) {
        return isValidEncryptionType(value);
    };
}

function isMacAddressString(value) {
    return (/^([0-9a-fA-F][0-9a-fA-F][\-\:]){5}([0-9a-fA-F][0-9a-fA-F])$/).test(value);
}

function isIpAddressString(value) {
    // http://regexlib.com/REDetails.aspx?regexp_id=2685
    return (/^(25[0-5]|2[0-4][0-9]|1[0-9][0-9]|[0-9]{1,2})(\.(25[0-5]|2[0-4][0-9]|1[0-9][0-9]|[0-9]{1,2})){3}$/).test(value);
}

function isSubnetMaskString(value) {
    // http://stackoverflow.com/questions/5360768/regular-expression-for-subnet-masking
    return (/^(((255\.){3}(255|254|252|248|240|224|192|128|0+))|((255\.){2}(255|254|252|248|240|224|192|128|0+)\.0)|((255\.)(255|254|252|248|240|224|192|128|0+)(\.0+){2})|((255|254|252|248|240|224|192|128|0+)(\.0+){3}))$/).test(value);
}

// Answers a function that verifies that a parameter is an array, and that the type of each entry matches the given exemplar.

function makeObjectArrayValidator( validationObject, comment ) {
    comment = comment || 'Array validation';

    return function (arrayOfObjects) {
        ok( $.isArray(arrayOfObjects), comment + ': is array.' );
        $.each(arrayOfObjects, function (index, obj ) {
            validateObject( obj, validationObject, comment + ': [' + index + ']' );
        });
        return true;
    };
}

// ***************************************************************************************************
// Tests for the testing framework functions.
// ***************************************************************************************************

test( 'Test the validation functions themselves', function () {
    var f = makeEnumerationChecker( 1, 'foo' );
    ok( f(1 ));
    ok( f('foo'));
    ok( !f('bar'));
    ok( isIpAddressString('127.0.0.1') );
    ok( !isIpAddressString('0.0.1') );
    ok( !isIpAddressString('a') );
    ok( !isMacAddressString('a') );
    ok( isMacAddressString('01:01:01:01:01:01') );

});

test("Test validateObject testing function", function () {
    var testObject = {
        trueParam: true,
        booleanParam: false,
        integerParam: '3',
        nonNullString: 'abc',
        string: 'string',
        arrayOfThings: [ { number: '1' }, {number: '2'} ],
        arrayOfThingsToCheckValues: [ { number: '1' }, {number: '2'} ]
    };
    var validation = {
        trueParam: true,
        booleanParam: isBoolean,
        integerParam: isIntegerString,
        nonNullString: isFullString,
        string: 'string',
        arrayOfThings: makeObjectArrayValidator( { number: isIntegerString } ),
        arrayOfThingsToCheckValues: [ { number: '1' }, {number: '2'} ]
    };
    validateObject( testObject, validation, 'validateObject test');
});


