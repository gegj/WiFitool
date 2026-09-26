require.config({
    paths: {
        underscore: 'lib/underscore_loader',
        'config/config': '../../js/config/config',
        'config/ufi/mf93d/config': '../../js/config/ufi/mf93d/config',
        'config/ufi/mf92/config': '../../js/config/ufi/mf92/config',
        'config/ufi/mf823/config': '../../js/config/ufi/mf823/config',
        'config/lte_config': '../../js/config/lte_config',
        'i18n': '../../js/lib/jquery/jquery.i18n.properties-1.0.9',
        simulate: '../../js/simulate',
        service: '../../js/service',
        smsData: '../../js/smsData',
        TestService: '../../test/js-test/qunit/TestServiceOnly',
        util: '../../js/util'
    }
});

require(['service', 'TestService', 'util', 'i18n']);