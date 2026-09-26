define(
		[ 'service', 'simulate', 'underscore' ],
		function(service, simulate, _) {
			/**
			 * Tests for Vendor API (c) Penrillian Ltd 2012
			 */

			// Initial setup:
			// SIM PIN must be 1234
			var myPhoneNumber = '+12345678'; // Vendor please change for the
			// SMS
			// tests to the correct phone number.

			// Many of the Vendor functions use a callback, so we use asyncTest.
			// Note
			// that these tests will be ignored when
			// using jsTestDriver (i.e. during build testing).

			(function ServiceOnlyTests() {
				var TimeBeforeAllCallbacksHaveHappenedMillis = 1000;

				module('ServiceOnly', {
					setup : function() {
						simulate.testEnv = true;
					},
					teardown : function() {
						// Vendor.SetAPICallbackObject(window);
					}
				});

				
				
				
				
				test('getDeviceInfo',function(){
					var data = service.getDeviceInfo();
					//log(data);
					checkMatches(/[0-9a-zA-Z._]+/, data.fw_version);
					checkMatches(/[0-9a-zA-Z._]+/, data.hw_version);
					checkMatches(/[0-9]+/, data.imei);
					ok(isIpAddressString(data.ipAddress),'check GatewayAdress');
					checkMatches(/[a-zA-Z]/,data.lanDomain);
					ok(isMacAddressString(data.macAddress),'check MacAdress');
					checkMatches(/[0-9]+/, data.max_access_num);
					checkMatches(/[0-9]+/, data.passPhrase);
					//checkMatches(/[0-9]+/, data.authMode);
					checkMatches(/^[0-9a-zA-Z][\s._\-0-9a-zA-Z]{7,63}$/, data.ssid);
					checkMatches(/[0-9a-zA-Z.]+/, data.sw_version);
				});
				
				
				test('getConnectionInfo', function() {
					var result = service.getConnectionInfo();
					ok(result, 'getConnectionInfo');
					//log(result);
					checkMatches(/[a-zA-Z]+/, result.connectStatus);
					validateObject(result.data_counter, {
						uploadRate : isInteger,
						downloadRate : isInteger,
						totalSent : isInteger,
						totalReceived : isInteger,
						totalConnectedTime : isInteger,
						currentSent : isInteger,
						currentReceived : isInteger,
						currentConnectedTime : isInteger,
						month : isString,
						monthlyConnectedTime : isInteger,
						monthlyReceived : isInteger,
						monthlySent : isInteger
					}, 'check ConnectionInfo');
				});	
				
				
				
				asyncTest('asyncTest Get/Set WifiBasic', function() {
					var wifiBasic = service.getWifiBasic();
					//log(wifiBasic);
					checkMatches(/^[0-9a-zA-Z][\s._\-0-9a-zA-Z]{0,31}$/,
							wifiBasic.SSID);
					checkMatches(/1|0/,
							wifiBasic.broadcast);
                    checkMatches(/NONE|WEP|WPAPSKWPA2PSK/,
                        wifiBasic.AuthMode);
                    checkMatches(/^[0-9a-zA-Z][\s._\-0-9a-zA-Z]{7,63}$/,
                        wifiBasic.passPhrase);
					var settingsToSet = {
						SSID : 'wangle',
						broadcast : true,
                        AuthMode : 'WPAPSKWPA2PSK',
                        passPhrase : '12098765323',
                        m_cipher : 2,
                        m_station : '3'
					};
					service.setWifiBasic(settingsToSet, function(data) {
                          ok(data.result == 'success');                    
					var result = service.getWifiBasic();
					 //log(result);
					ok(result, 'SetWifiBasic');
					  equals(result.AuthMode,settingsToSet.AuthMode);
					  equals(result.MAX_Access_num,settingsToSet.station);
					  equals(result.SSID,settingsToSet.SSID);
					  equals(result.broadcast,settingsToSet.broadcast);
					  equals(result.passPhrase,settingsToSet.passPhrase);
					//validateObject(result, settingsToSet);
					start();
				});
			});

			
				
				asyncTest('asyncTest Get/Set WifiAdvance', function() {
					var WifiAdvance = service.getWifiAdvance();									
					checkMatches(/([0-4]{1})/, WifiAdvance.bandwidth);
                    checkMatches(/([0-4]{1})/, WifiAdvance.mode);
//					checkMatches(/^[0-9a-zA-Z][\s._\-0-9a-zA-Z]{7,63}$/,
//							WifiAdvance.passPhrase);
                    checkMatches(/[a-zA-Z]/, WifiAdvance.countryCode);
                    checkMatches(/[0-9]+/, WifiAdvance.channel);
                    checkMatches(/[0-9]+/, WifiAdvance.rate);
//                    checkMatches(/[0-9]+/, WifiAdvance.maxStation);
//                    checkMatches(/[0-9]+/, WifiAdvance.station);
					var settingsToSet = {
                        mode : '3',
                        countryCode : 'cn-tw',
                        channel : '2',
                        rate : '1',
                        wifiBand : 'b',
                        bandwidth : '1'
					};
					service.setWifiAdvance(settingsToSet,function(result){
						ok(result, 'SetWifiAdvance');
					var data = service.getWifiAdvance();
						log(data);
//	                    $.extend(settingsToSet, {
//	                        maxStation : isIntegerString
//	                    });
						validateObject(data, settingsToSet);
						start();
					});										
				});

				asyncTest('asyncTest setWifiBasicMultiSSIDSwitch', function() {
					param = {};
					param.multi_ssid_enable = '1';
					service.setWifiBasicMultiSSIDSwitch(param, function(result){
						ok(result.result == "success");
						param.multi_ssid_enable ='0';
						service.setWifiBasicMultiSSIDSwitch(param, function(result){
							ok(result.result == "success");
							start();
						});						
					});	
				});					
	

				asyncTest('asyncTest setWifiBasic4SSID2', function() {
					var settingsToSet = {
							m_SSID : 'zhangfei',
							m_broadcast : true,
	                        m_AuthMode : 'WPAPSKWPA2PSK',
	                        m_passPhrase : '987654321',
	                        m_cipher : 2,
	                        m_station : '3'
						};
					service.setWifiBasic4SSID2(settingsToSet, function(result){
						ok(result.result == "success");		
					var data = service.getWifiBasic();
					  //  log(data);
					    equals(data.m_AuthMode,settingsToSet.m_AuthMode);
					    equals(data.m_MAX_Access_num,settingsToSet.m_station);
					    equals(data.m_SSID,settingsToSet.m_SSID);
					    equals(data.m_broadcast,settingsToSet.m_broadcast);
					    equals(data.m_passPhrase,settingsToSet.m_passPhrase);
						start();											
					});	
				});		
				
				
				asyncTest('asyncTest Get/Set Language', function() {
					var LanguageKey = service.getLanguage();
					ok(LanguageKey, 'Language');
					checkMatches(/en|zh-cn/, LanguageKey.Language);
					var settingsToSet = {
						Language : 'en'
					};
					service.setLanguage(settingsToSet,function(result){
						ok(result, 'SetLanguage success');
						var data = service.getLanguage();						
						validateObject(data, settingsToSet);
						start();
					});					
				});

			
				
				
				asyncTest('asyncTest Get AttachedDevices', function() {
					setTimeout(function() {
						var AttachedDeviceInfo = service.getCurrentlyAttachedDevicesInfo().attachedDevices;
						for ( var i = 0; i < AttachedDeviceInfo.length; i++) {
							validateObject(AttachedDeviceInfo[i], {
								macAddress : isMacAddressString,
								hostName : isFullString,
								ipAddress : isMacAddressString,
								timeConnected : isInteger
							}, 'check AttachedDevices');
						}
						start();
					}, 4000);
				});
				
				
				asyncTest('asyncTest setBearerPreference', function() {
					var settingsToSet = {
						strBearerPreference : 'Only_WCDMA'
					};
					service.setBearerPreference(settingsToSet,function(result){						
						ok(result.result == 'success','setBearerPreference success');
						start();
						/*checkMatches(
								/LTE_preferred|Only_LTE|2G Only|Only_GSM|3G Only|Only_WCDMA|3G Preferred|WCDMA_preferred/,
								settingsToSet.strBearerPreference);*/
					});					
					/*
					 * var result = service.getBearerPreference(); ok(result,
					 * 'getBearerPreference'); //log(result);
					 * $.extend(settingsToSet, { isTest : isBoolean, goformId :
					 * isString }); validateObject(result, settingsToSet);
					 */
				});
				
				

			asyncTest('asyncTest setNetwork', function() {
					var params = {};
					params.strNetworkNumber = "46001";
					params.nRat = 0;
					service.setNetwork(params.strNetworkNumber, params.nRat, function(success){
						var register = false;
						if (success) {
							register = true;
						}
						ok(register, "setNetwork");
						start();
					});					
				});

			
			
			
			
				asyncTest(
						'asyncTest getStatusInfo',
						function() {
							setTimeout(
								function() {
									var result = service.getStatusInfo();				
									ok(result, 'getStatusInfo');	
									//log(result);
									checkMatches(/[a-zA-Z]+/, result.authMode);
									checkMatches(/[0-9]+/, result.batteryLevel);
									checkMatches(/0|1/, result.batteryStatus);
									checkMatches(/[0-9]+/, result.batteryTime);
									checkMatches(/ppp_disconnected|ppp_connected|ppp_connecting|ppp_disconnecting/,
											result.connectStatus);
									delete result.data_counter.month;
									delete result.data_counter.monthlyConnectedTime;
									delete result.data_counter.monthlyReceived;
									delete result.data_counter.monthlySent;
									validateObject(result.data_counter, {
										uploadRate : isInteger,
										downloadRate : isInteger,
										//totalSent : isInteger,
										//totalReceived : isInteger,
										//totalConnectedTime : isInteger,
										currentSent : isInteger,
										currentReceived : isInteger,
										currentConnectedTime : isInteger
									}, 'check ConnectionInfo');
									checkMatches(/true|false/, result.isLoggedIn);
									ok(isChn(result.networkOperator),'check networkOperator');									
									checkMatches(/GSM|GPRS|EDGE|WCDMA|HSDPA|HSPA|HSPA+|DC-HSPA+|LTE/,
											result.networkType);
									checkMatches(/[0-9]+/, result.pinStatus);
									checkMatches(/[0-9]/, result.signalImg);
									checkMatches(/true|false/, result.roamingStatus);
									checkMatches(
											/modem_waitpin|modem_sim_undetected|modem_imsi_waitnck|modem_sim_destroy|modem_init_complete|modem_waitpuk/,
											result.simStatus);						
//									checkMatches(/[0-9]+/, result.smsNvCapability);
//									checkMatches(/[0-9]+/, result.smsNvCapabilityUsed);
									checkMatches(/^[0-9a-zA-Z\s._\-][\s._\-0-9a-zA-Z]{0,31}$/, result.ssid);
									checkMatches(/true|false/, result.wifiStatus);
//									for ( var i = 0; i < result.attachedDevices.length; i++) {
//										validateObject(result.attachedDevices[i], {
//											macAddress : isMacAddressString,
//											hostName : isFullString,
//											ipAddress : isMacAddressString,
//											timeConnected : isInteger
//										}, 'check AttachedDevices');
//									}
									start();
								}, 2000);
						});

			

				
				/*  asyncTest('getCurrentNetwork', function() {						
							service.getCurrentNetwork(function(result,currentNetwork){
								ok(result);
								log(result);
								start();
							});						
				});*/
				 

				/*
				 * test('disconnect',function(){ var data =
				 * service.disconnect();
				 * log("&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&&"); log(data); });
				 */

				test('getNetSelectInfo', function() {
					var NetSelectInfo = service.getNetSelectInfo();
					//log(NetSelectInfo);
					checkMatches(/.+/, NetSelectInfo.current_network_mode);
					checkMatches(/.+/, NetSelectInfo.m_netselect_status);
					checkMatches(/.+/, NetSelectInfo.m_netselect_save);
					checkMatches(/^[0-9][0-9a-zA-Z\s\(\),";]+[0-9]$/,NetSelectInfo.m_netselect_contents);
					checkMatches(/[a-zA-Z_]+/, NetSelectInfo.net_select);
					checkMatches(/ppp_disconnected|ppp_connected|ppp_connecting|ppp_disconnecting/,
							NetSelectInfo.ppp_status);
					checkMatches(/modem_sim_undetected|modem_imsi_waitnck|modem_sim_destroy|modem_init_complete|modem_waitpin|modem_waitpuk/, NetSelectInfo.modem_main_state);
				});

				asyncTest('asyncTest getPhoneBookReady',function(){
					service.getPhoneBookReady({},function(data){
						ok(isIntegerString(data.pbm_init_flag));
						start();
					});
				});

				test('getSIMPhoneBookCapacity', function() {
					var data = service.getSIMPhoneBookCapacity();
					validateObject(data, {
						simPbmTotalCapacity : isInteger,
						simPbmUsedCapacity : isInteger,
						simType : isString,
						maxNameLen : isInteger,
						maxNumberLen : isInteger
					}, 'check SIMPhoneBookCapacity');
				});

				test('getDevicePhoneBookCapacity', function() {
					var data = service.getDevicePhoneBookCapacity();
					validateObject(data, {
						pcPbmTotalCapacity : isInteger,
						pcPbmUsedCapacity : isInteger
					}, 'check DevicePhoneBookCapacity');
				});

				test('getPhoneBooks',function(){
					var sim = service.getSIMPhoneBookCapacity();
					var device = service.getDevicePhoneBookCapacity();
					var capacity = sim.simPbmUsedCapacity + device.pcPbmUsedCapacity; 
					var para = {};
					para.page = 0;
					para.data_per_page = capacity;
					para.orderBy = "name";
					para.isAsc = true;
					para.multi_data = 1; 
					var books = service.getPhoneBooks(para);
					if($.isArray(books.pbm_data) && books.pbm_data.length > 0)
						{
						for(var i = 0;i<books.pbm_data.length;i++)
							{ok(isString(books.pbm_data[i].pbm_name),'get pbm_name');
						    ok(isIntegerString(books.pbm_data[i].pbm_number),'get pbm_number');
						    ok(isInteger(books.pbm_data[i].pbm_id),'get pbm_id');
						    ok(isIntegerString(books.pbm_data[i].pbm_location),'get pbm_location');
							}
						}					
				});
				
				
				test('getPhoneBooksByGroup',function(){
					var para = {};
					para.group = "family";
				    para.isAsc = true;
				    para.data_per_page = 150;
				    para.page = 0;
				    para.orderBy = "name";
				    var books = service.getPhoneBooksByGroup(para);
				    //log(books);
				    for(var i = 0;i<books.pbm_data.length;i++)
					{
				    ok(isString(books.pbm_data[i].pbm_name),'get pbm_name');
				    ok(isIntegerString(books.pbm_data[i].pbm_number),'get pbm_number');
				    ok(isInteger(books.pbm_data[i].pbm_id),'get pbm_id');
				    ok(isIntegerString(books.pbm_data[i].pbm_location),'get pbm_location');
				    ok(books.pbm_data[i].pbm_group == 'family','check group');
					}				  
				});
				
				
				asyncTest('asyncTest deleteAllPhoneBooksByGroup', function() {
					var params = {};
					params.location = 3;
					params.group = 'common'; 
					service.deleteAllPhoneBooksByGroup(params,function(result){					
						ok(result.result == 'success');
						var para = {};
						para.group = "common";
					    para.isAsc = true;
					    para.data_per_page = 150;
					    para.page = 0;
					    para.orderBy = "name";
						var data = service.getPhoneBooksByGroup(para);
						ok(data.pbm_data.length == 0,'deleteAllPhoneBooksByGroup success');
						start();
					});
				});
				
				
				asyncTest('asyncTest Get/saveDevicePhoneBook', function() {
                    var para = {};
                    para.page = 0;
                    para.data_per_page = 1000;
                    para.orderBy = "name";
                    para.isAsc = true;
                    para.multi_data = 1;
					var origData = service.getDevicePhoneBooks(para);
					var settingsToSet = {
						index : -1,
						location : 1,
						name : "wangle",
						mobile_phone_number : "18891006121",
						home_phone_number : "15900290130",
						office_phone_number : "8816",
						mail : "wangle@yeah.net",
						group : "common"
					};
					service.savePhoneBook(settingsToSet,function(result){
						ok(result.result = 'success','savePhoneBook succeed');
						//log(result);
						var data = service.getDevicePhoneBooks(para);
						//log(data);
						equals(data.pbm_data.length,
								origData.pbm_data.length + 1,
								"phoneBooks record number");								
						//log(data.pbm_data[data.pbm_data.length-1]);
						equals(data.pbm_data[data.pbm_data.length-1].pbm_location,settingsToSet.location);
						equals(data.pbm_data[data.pbm_data.length-1].pbm_name,settingsToSet.name);
						equals(data.pbm_data[data.pbm_data.length-1].pbm_number,settingsToSet.mobile_phone_number);
						equals(data.pbm_data[data.pbm_data.length-1].pbm_anr,settingsToSet.home_phone_number);
						equals(data.pbm_data[data.pbm_data.length-1].pbm_anr1,settingsToSet.office_phone_number);
						equals(data.pbm_data[data.pbm_data.length-1].pbm_email,settingsToSet.mail);
						equals(data.pbm_data[data.pbm_data.length-1].pbm_group,settingsToSet.group);
						start();
					});					
				});

				asyncTest('asyncTest Get/setConnectionMode', function() {
					var GetconnectionMode = service.getConnectionMode();
					ok(GetconnectionMode, 'Get/setConnectionMode');
					checkMatches(/auto_dial|manul_dial/,
							GetconnectionMode.connectionMode);
					var settingsToSet = {
						connectionMode : '1',
                        isAllowedRoaming: "off"
					};
					service.setConnectionMode(settingsToSet, function(result){
				      ok(result.result == 'success');
				      var data = service.getConnectionMode();
						ok(data, 'Get/setConnectionMode');
						validateObject(data, settingsToSet);
						start();
					});					
				});
				
				asyncTest('asyncTest deleteDevicePhoneBooks', function() {
                    var para = {};
                    para.page = 0;
                    para.data_per_page = 1000;
                    para.orderBy = "name";
                    para.isAsc = true;
                    para.multi_data = 1;
					var origData = service.getDevicePhoneBooks(para);
					//log(origData);
					ok(origData.pbm_data.length, "Phone books size " + origData.pbm_data.length);
					if(origData.pbm_data.length == 0)
						{						
						var settingsToSet = {
								index : -1,
								location : 1,
								name : "jiangnan",
								mobile_phone_number : "18891006120",
								home_phone_number : "15900290131",
								office_phone_number : "8816",
								mail : "jiangnan@yeah.net",
								group : "common"
							};
							service.savePhoneBook(settingsToSet,function(result){
								ok(result.result = 'success','savePhoneBook succeed');
								//log(result);
								var data = service.getDevicePhoneBooks(para);
								//log(data);
								equals(data.pbm_data.length,
										origData.pbm_data.length + 1,
										"phoneBooks record number");
								start();
							});						
						}
					else{
					var params = {};
					params.indexs = [ String(origData.pbm_data[origData.pbm_data.length - 1].pbm_id) ];
					result = service.deletePhoneBooks(params,function(result){
						ok(result.result == "success", "delete phone book item");
						var data = service.getDevicePhoneBooks(para);
						ok(data.pbm_data.length, "Phone books deleted size " + data.pbm_data.length);
						var macths = false;
						if (origData.pbm_data.length - data.pbm_data.length === 1) {
							macths = true;
						}
						ok(macths, 'delete device PhoneBooks');
						start();					
					});
					}
				});

				asyncTest('asyncTest deleteSimPhoneBooks', function() {
                    var para = {};
                    para.page = 0;
                    para.data_per_page = 1000;
                    para.orderBy = "name";
                    para.isAsc = true;
                    para.multi_data = 1;
					var origData = service.getSIMPhoneBooks(para);
					ok(origData.pbm_data.length, "Phone books size " + origData.pbm_data.length);
					if(origData.pbm_data.length == 0)
						{
						var settingstoSet = {
								location: 0,
								index: -1,
								name: "zhangyang",
								mobile_phone_number: "19900998878"
						};
						service.savePhoneBook(settingsToSet,function(result){
							ok(result.result = 'success','savePhoneBook succeed');
							//log(result);
							var data = service.getSIMPhoneBooks(para);
							//log(data);
							equals(data.pbm_data.length,
									origData.pbm_data.length + 1,
									"phoneBooks record number");
							start();
						});	
						}
					else{
					var params = {};
					params.indexs = [ String(origData.pbm_data[origData.pbm_data.length - 1].pbm_id) ];
				    service.deletePhoneBooks(params,function(result){
				    	ok(result.result == "success", "delete phone book item");
						var data = service.getSIMPhoneBooks(para);
						ok(data.pbm_data.length, "Phone books deleted size " + data.pbm_data.length);
						var macths = false;
						if (origData.pbm_data.length - data.pbm_data.length === 1) {
							macths = true;
						}
						ok(macths, 'delete sim PhoneBooks');
						start();
					});		
					}
				});

				asyncTest('asyncTest deleteDeviceAllPhoneBooks', function() {
					var params = {
						location : 1
					};
					service.deleteAllPhoneBooks(params,function(result){
						ok(result);
						 var para = {};
		                    para.page = 0;
		                    para.data_per_page = 1000;
		                    para.orderBy = "name";
		                    para.isAsc = true;
		                    para.multi_data = 1;
						var data = service.getDevicePhoneBooks(para);
						//log(data);
						ok(data.pbm_data.length == 0, 'delete AllPhone Books');
						start();
					});
				});

				asyncTest('asyncTest deleteSimAllPhoneBooks', function() {
					var params = {
						location : 0
					};
					service.deleteAllPhoneBooks(params,function(result){
						ok(result);
						var para = {};
	                    para.page = 0;
	                    para.data_per_page = 1000;
	                    para.orderBy = "name";
	                    para.isAsc = true;
	                    para.multi_data = 1;
	                    var data = service.getSIMPhoneBooks(para);
						ok(data.pbm_data.length == 0, 'delete Sim AllPhone Books succeed');
						start();
					});                    					
				});

				test('getApnSettings', function() {
					var ApnSettings = service.getApnSettings();
					//log(ApnSettings);
					ok(ApnSettings, 'getApnSettings');
					checkMatches(/[0-9a-zA-Z]+/, ApnSettings.profileName);
					checkMatches(/[0-9a-zA-Z]+/, ApnSettings.wanApn);
					checkMatches(/auto|manual/, ApnSettings.dnsMode);
					// checkMatches(/[0-9.]+/, ApnSettings.dns1);
					// checkMatches(/[0-9.]+/, ApnSettings.dns2);
					checkMatches(/none|pap|chap/, ApnSettings.authMode);
					checkMatches(/[0-9a-zA-Z]+/, ApnSettings.username);
					checkMatches(/[0-9a-zA-Z]+/, ApnSettings.password);
				});
				
			asyncTest('asyncTest addOrEditApn/deleteApn', function() {
					// add APN
					var settingsToSet = {
						index : 3,
						profileName : "zte",
						wanApn : "cmnet",
						dnsMode : "auto",
						dns1 : "",
						dns2 : "",
						authMode : "8816",
						username : "wangle_zte",
						password : "friend"
					};
					service.addOrEditApn(settingsToSet, function(result1){
						ok(result1.result);
						var getapn = service.getApnSettings();
						var keys = getapn.APNs.split("||");
						var match = false;
						var i = 0;
						for ( ; i < keys.length; i++) {
							var name = keys[i].split("($)")[0];
							if (name == settingsToSet.profileName) {
								match = true;
								break;
							}
						}
						ok(match, "add Apn is succeed");
						
						// edit APN
						var settingsToSet2 = {
							index : i,
							oldProfileName : "zte",
							profileName : "telecom",
							wanApn : "cdma",
							dnsMode : "auto",
							dns1 : "",
							dns2 : "",
							authMode : "881",
							username : "wangle",
							password : "common"
						};
						service.addOrEditApn(settingsToSet2, function(result2){
							ok(result2.result);
							getapn = service.getApnSettings();
							keys = getapn.APNs.split("||");
							match = false;
							var j = 0;
							for ( ; j < keys.length; j++) {
								if (keys[j].split("($)")[0] == settingsToSet2.profileName) {
									match = true;
									break;
								}
							}
							ok(match, "edit APN is succeed");
							// delete APN
							var deleteprofile = {
								profileName : settingsToSet2.profileName,
								index: j,
								apnMode: 'manual'
							};
							service.deleteApn(deleteprofile, function(result3){
								ok(result3.result);
								getapn = service.getApnSettings();
								keys = getapn.APNs.split("||");
								match = true;
								for ( var i = 0; i < keys.length; i++) {
									var name = keys[i].split("($)")[0];
									if (name == deleteprofile.profileName) {
										match = false;
										break;
									}
								}
								ok(match, "delete APN is succeed");
								start();
							});
						});
					});
				});
				
				
				
				asyncTest('asyncTest setDefaultApn', function() {
					var getapn = service.getApnSettings();
					//log(getapn);
					var keys = getapn.APNs.split("||");
					var items = [];
					if(keys.length > 0){
						items = keys[0].split("($)");
					}
					var settingsToSet = {};
					settingsToSet.index = 0;
					settingsToSet.apnMode = 'manual';
					settingsToSet.profileName = items[0];
					settingsToSet.wanApn = items[1];
					settingsToSet.authMode = items[4];
					settingsToSet.username = items[5];
					settingsToSet.password = items[6];
					settingsToSet.dnsMode = items[10];
					settingsToSet.dns1 = items[11];
					settingsToSet.dns2 = items[12];
					
					service.setDefaultApn(settingsToSet, function(data){
						ok(data.result, "Set Default Apn");
						var apn = service.getApnSettings();
						//log(apn);
						//IPv6的APN设置修改中，暂时屏蔽判断
						//ok(apn.currIndex == 0, "Set Default Apn Success");
						start();
					});
				});

				test('getSIMPhoneBookCapacity', function() {
					var data = service.getSIMPhoneBookCapacity();
					validateObject(data, {
						simPbmTotalCapacity : isInteger,
						simPbmUsedCapacity : isInteger,
						simType : isString,
						maxNameLen : isInteger,
						maxNumberLen : isInteger
					}, 'check SIMPhoneBookCapacity');
				});

				test('getDevicePhoneBookCapacity', function() {
					var data = service.getDevicePhoneBookCapacity();
					validateObject(data, {
						pcPbmTotalCapacity : isInteger,
						pcPbmUsedCapacity : isInteger
					}, 'check DevicePhoneBookCapacity');
				});

				test('getLoginData', function() {
					var data = service.getLoginData();
					validateObject(data, {
						modem_main_state : isString,
						pinnumber : isInteger,
						puknumber : isInteger,
						admin_Password : isString
					}, 'check DevicePhoneBookCapacity');
				});

				asyncTest('asyncTest login/logout/getLoginStatus', function() {
					// var result = service.getLoginStatus();
					// log(result);
					service.login({
						password : '123456'
					}, function(success) {
						ok(success.result, 'login succeed');
						var result1 = service.getLoginStatus();
						ok(result1.status == 'loggedIn', 'getLoginStatus: ' + result1.status);
						service.logout({}, function(success) {
							ok(success.result, 'logout succeed');
							var result2 = service.getLoginStatus();
							ok(result2.status == 'loggedOut', 'getLoginStatus: ' + result2.status);
							start();
						});
					});
				});
				
				asyncTest('asyncTest enter incorrect PIN/PUK', function() {
					service.enterPIN({
						PinNumber : 4321
					}, function(back) {
						ok(back.result == false);
						var data = service.getLoginData();
						ok(data.pinnumber == 2 && data.modem_main_state == 'modem_waitpin', 'remanin 2 times');
						service.enterPIN({
							PinNumber : 5321
						}, function(back) {
							ok(back.result == false);
							var data = service.getLoginData();
							ok(data.pinnumber == 1 && data.modem_main_state == 'modem_waitpin', 'remanin 1 times');
							service.enterPIN({
								PinNumber : 6321
							}, function(back) {
								ok(back.result == false);
								var data = service.getLoginData();
								ok(data.pinnumber == 0 && data.modem_main_state == 'modem_waitpuk', 'enter PUK');

								var params = {};
								params.PinNumber = 1234;
								params.PUKNumber = 12345679;
								service.enterPUK(params, function(success) {
									ok(!success.result);
									var data = service.getLoginData();
									ok(data.modem_main_state == 'modem_waitpuk' && data.puknumber == 9,
											'modem_main_state:  ' + data.modem_main_state);
									params.PUKNumber = 11111111;
									service.enterPUK(params, function(success) {
										ok(success.result);
										var data = service.getLoginData();
										ok(data.modem_main_state == 'modem_init_complete', 'modem_main_state:  '
												+ data.modem_main_state);
										start();
									});
								});
							});
						});
					});
				});
				
				
				
				asyncTest('asyncTest enterPIN', function() {
					service.enterPIN({
						PinNumber : 1234
					}, function(back) {
						ok(back.result);
						var data = service.getLoginData();
						var flag = false;
						if (data.modem_main_state !== 'modem_waitpin') {
							flag = true;
						}
						ok(flag, 'enter correct PIN');
						start();
					});
				});
				
				asyncTest('asyncTest getSmsCapability', function(){
					service.getSmsCapability({}, function (capability) {
				    ok(capability.nvTotal >0, 'simcard capability: '+capability.nvTotal);		
						start();
					});
				});
				
				
				asyncTest('asyncTest getSMSReady', function(){
					service.getSMSReady({}, function (data) {
						ok(isIntegerString(data.sms_cmd_status_result));				
						start();
					});
				});
				
				
				
				asyncTest('asyncTest setSmsRead', function() {
					service.getSMSMessages({
						page : 0,
						smsCount : 500,
                        nMessageStoreType : "1",
						orderBy : "order by id desc",
                        tags: "10"
					}, function(data1) {
						var result1 = data1.messages; 
						ok(_.size(result1) > 0, 'Get message: ' + _.size(result1));
						var evens = _.filter(result1, function(object) {
							return object.isNew === true;
						});
						ok(evens, 'Unread message exist');
						var para = {};
						para.ids = _.pluck(evens, 'id');
						service.setSmsRead(para, function(data) {
							ok(data.result);
							service.getSMSMessages({
                                page : 0,
                                smsCount : 500,
                                nMessageStoreType : "3",
                                orderBy : "order by id desc",
                                tags: "10"
							}, function(data2) {
								// log(result2);
								var result2 = data2.messages; 
								var num = _.pluck(result2, 'isNew');
								ok(_.all(num, function(n) {
									return n == false;
								}), 'set SMSread success');
								start();
							});
						});
					}, function(error) {
						ok(false, "Get Message fail");
						start();
					});

				});																			

				asyncTest('asyncTest sendSMS', function() {
					service.getSMSMessages({
						page : 0,
						smsCount : 500,
						nMessageStoreType : 1,
						tags : 10,
						orderBy : "order by id desc"
					}, function(data1) {
						var result1 = data1.messages;
						ok(_.size(result1), 'Get message: ' + _.size(result1));
						service.sendSMS({
							number : 13900000000,
							message : "OK",
							id : -1
						}, function(success) {
							ok(success.result, 'sendSMS success');

							service.getSMSMessages({
								page : 0,
								smsCount : 500,
								nMessageStoreType : 1,
								tags : 10,
								orderBy : "order by id desc"
							}, function(data2) {
								var result2 = data2.messages;
								ok(_.size(result2) - _.size(result1) === 1, 'send SMSmessage success: '
										+ _.size(result2));
								start();
							});
						}, function(error) {
							ok(false, "send Message fail");
							start();
						});
					});
				});

				
				function getCurrentTimeString(theTime) {
					var time = "";
					var d = theTime ? theTime : new Date();
					time += (d.getFullYear() + "").substring(2) + ";";
					time += getTwoDigit((d.getMonth() + 1)) + ";" + getTwoDigit(d.getDate()) + ";" + getTwoDigit(d.getHours()) + ";"
					+ getTwoDigit(d.getMinutes()) + ";" + getTwoDigit(d.getSeconds()) + ";";
					if (d.getTimezoneOffset() < 0) {
					time += "+" + (0 - d.getTimezoneOffset() / 60);
					} else {
					time += (0 - d.getTimezoneOffset() / 60);
					}
					return time;
					} 
				
				
				asyncTest('asyncTest saveSMS', function() {
					
					service.getSMSMessages({
						page : 0,
						smsCount : 500,
						nMessageStoreType : 3,
						tags : 10,
						orderBy : "order by id desc"									
					},function(result1){
						//log(result1);																
					var datetime = new Date();
					var params = {
							index: -1,
							currentTimeString: getCurrentTimeString(datetime),
							groupId: '',
							message: 'are you ok',
							numbers: ['13512323366']
							}; 
					service.saveSMS(params, function(result){
						ok(result);						
						service.getSMSMessages({
							page : 0,
							smsCount : 500,
							nMessageStoreType : 3,
							tags : 10,
							orderBy : "order by id desc"
						}, function(result2){
							//log(result2);
							ok(result2.messages.length - result1.messages.length === 1, 'saveSMS success');
							start();
						 });
					   });											
					});
				});
				
				
				
				asyncTest('asyncTest deletephonebook_SMSMessages', function() {
					service.getSMSMessages({
						page : 0,
						smsCount : 500,
						nMessageStoreType : 1,
						tags : 10,
						orderBy : "order by id desc"
					}, function(data1) {
						var result1 = data1.messages;
						ok(_.size(result1) > 0, 'getSMSMessages Size: '
								+ _.size(result1));												
						var num1 = _.pluck(result1, 'number');						
						var evens = _.filter(result1, function(object) {
							return object.number === num1[num1.length - 1];
						});						
						var id = _.pluck(evens, 'id');						
						service.deleteMessage({
							ids : id,
							location : "native_inbox"
						}, function(success) {
							ok(success.result);
							service.getSMSMessages({
								page : 0,
								smsCount : 500,
								nMessageStoreType : 1,
								tags : 10,
								orderBy : "order by id desc"
							},
							function(data2){
								var result2 = data2.messages;
								ok(_.size(result2) < _.size(result1), 'getSMSMessages Size: '+ _.size(result2));
									var num2 = _.pluck(result2, 'number');
									ok($.inArray(num1[num1.length - 1],
											num2) == -1, 'delete success');
									start();
								});							
						}, function(error) {
							errorOverlay(error.errorText);
							start();
						});																
					}, function(error) {
						ok(false, "Get Message fail");
						start();
					});										
				});
            
				
			asyncTest('asyncTest deleteSingleMessage', function() {
				service.getSMSMessages({
					page : 0,
					smsCount : 500,
					nMessageStoreType : 1,
					tags : 10,
					orderBy : "order by id desc"
				}, function(data1) {
					var result1 = data1.messages;
					ok(_.size(result1) > 0, 'getSMSMessages Size: '
							+ _.size(result1));
					service.deleteMessage(set = {
							ids : [result1[getRandomInt(_.size(result1)-1)].id],
							location : "native_inbox"
						}, function(success) {						
							ok(success.result,"delete success");
							service.getSMSMessages({
								page : 0,
								smsCount : 500,
								nMessageStoreType : 1,
								tags : 10,
								orderBy : "order by id desc"
							}, function(data2) {
								var result2 = data2.messages;
								ok(_.size(result1) - _.size(result2) === 1, 'getSMSMessages Size: '
								     + _.size(result2));
								start();		
							});												
						});					
				}, function(error) {
					ok(false, "Get Message fail");
					start();
				});					
			});
				
				
				/*function deleteMessage(set) {
					var ids = set.msg_id.split(";");
					smsArr.messages = $.grep(smsArr.messages, function(
							n, i) {
						return $.inArray(n.id + "", ids) == -1;
					});
				}*/
				
				
				
			   
			/*	asyncTest('asyncTest deleteAllMessages', function() {
					service.deleteAllMessages({
						location : "native_inbox"
					}, function(success) {
						if (success.result) {
							ok(success.result, "delete success");
							service.getSMSMessages({
								page : 1,
								smsCount : 1000,
								nMessageStoreType : "native_inbox",
								orderBy : "date",
								isAsc : false
							}, function(data) {
								ok(!data.messages.length, "Messages length");
								start();
							}, function(error) {
								ok(false, "Get Message fail");
								start();
							});
						} else {
							ok(isErrorObject(success), "get a error object");
							start();
						}
					});
				});*/

				asyncTest('asyncTest enablePin', function() {
					var para = {
						oldPin : '1234'
					};
					service.enablePin(para, function(data) {
						ok(data.result == true);
						service.getPinData({}, function(success) {
							ok(success);
							//log(success);
							ok(success.pin_status === '1', 'enablePin');
							start();
						});
					});
				});

				asyncTest('asyncTest changePin', function() {
					var para = {
						oldPin : '1234',
						newPin : '4321'
					};
					service.changePin(para, function(success) {
						ok(success.result == true);
						start();
					});
				});

				asyncTest('asyncTest disablePin', function() {
					var para = {
						oldPin : '4321'
					};
					service.disablePin(para, function(data) {
						ok(data.result == true);
						service.getPinData({}, function(success) {
							ok(success);
							ok(success.pin_status === '0', 'disablePin');
							start();
						});
					});
				});

				asyncTest('asyncTest changePassword', function() {
					var para = {
						oldPassword : 'admin',
						newPassword : '654321'
					};
					service.changePassword(para, function(data) {
						ok(data.result == true);
						service.getLoginData({}, function(success) {
							ok(success);
							ok(success.admin_Password === '654321',
									'changePassword');
							start();
						});
					});
				});

				test('getLanInfo', function() {
					result = service.getLanInfo();
					//log(result);
					validateObject(result, {
						ipAddress : isIpAddressString,
						subnetMask : isSubnetMaskString,
						macAddress : isMacAddressString,
						dhcpServer : isFullString,
						dhcpStart : isIpAddressString,
						dhcpEnd : isIpAddressString,
						dhcpLease : isInteger
					}, 'check LanInfo');
				});

				test('setLanInfo', function() {
					var settingsToSet = {
						ipAddress : '192.168.1.1',
						subnetMask : '255.255.255.0',
						dhcpServer : '1',
						dhcpStart : '192.168.1.100',
						dhcpEnd : '192.168.1.200',
						dhcpLease : 24
					};
					service.setLanInfo(settingsToSet);
					result = service.getLanInfo();
					//log(result);
					$.extend(settingsToSet, {
						macAddress : isMacAddressString
					});
					validateObject(result, settingsToSet);
				});

			/*	test('get/set SmsSetting', function() {
					var settingsToSet = {
						validity : 'one_month',
						centerNumber : 1399998877,
						deliveryReport : '1'
					};
					service.setSmsSetting(settingsToSet);
					var setting = service.getSmsSetting();
					validateObject(setting, settingsToSet);

				});*/
				

				
				asyncTest('asyncTest restoreFactorySettings', function() {
					//TODO: 鍐欓敊浜�
					var result = service.restoreFactorySettings({}, function(
							data) {
						//log(result);
						ok(data.result == 'success');
						start();
					});
				});
				
			 asyncTest('asyncTest checkrestore', function() {
					setTimeout(function() {						
						service.checkRestoreStatus(function() {							
							ok(true, "restore success");
							start();
						});
					}, 5000);
				});
			
			
				
			 asyncTest('asyncTest openWps_PBC', function() {
					service.openWps({wpsType: "PBC"}, function(data) {
						ok(data.result == 'success');
					var result = service.getWpsInfo();
					log(result);
					ok(result.authMode == 'WPAPSKWPA2PSK','AuthMode: ' + result.authMode);
					ok(result.radioFlag == '1', 'RadioFlag: '+result.radioFlag);
					ok(result.wpsFlag == '1', 'wpsFlag: ' +result.wpsFlag);
						start();
					});
				}); 
			 
			 asyncTest('asyncTest openWps_PIN', function() {
				 var parms = {};
				 parms.wpsType = 'PIN';
					service.openWps(parms, function(data) {
						ok(data.result == 'success');
					var result = service.getWpsInfo();
					ok(result.authMode == 'WPAPSKWPA2PSK','AuthMode: ' + result.authMode);
					ok(result.radioFlag == '1', 'RadioFlag: '+result.radioFlag);
					ok(result.wpsFlag == '1', 'wpsFlag: ' +result.wpsFlag);
						start();
					});
				}); 
			 
			
			 
			 
			 asyncTest('asyncTest get/set SleepMode', function() {
				var params = {};
				 params.sleepMode = '15'; 
				service.setSleepMode(params,function(back){
					ok(back.result == 'success');
					service.getSleepMode({},function (data){
						ok(data.sleepMode == '15','get SleepMode: '+data.sleepMode);
						start();
					});
				});
				}); 
			 
			 
			 asyncTest('asyncTest set/get SysSecurity', function() {
				 var params = {};
				 params.remoteFlag = '1'; 
				 params.pingFlag = '1';
				service.setSysSecurity(params,function(data){
					ok(data.result == 'success');
						service.getSysSecurity({},function(result){
							ok(result);
							ok(result.pingFlag == '1','ping Internet enable: '+result.pingFlag);
							ok(result.remoteFlag == '1','remote manage enable: '+result.remoteFlag);
							start();
						});					
				    });
			     }); 
			 
			 asyncTest('asyncTest set/get PortForward', function() {
				 var data = service.getPortForward();
				 var params = {};
				 params.ipAddress = '192.168.0.12'; 
				 params.portStart = '23';
				 params.portEnd = '81';
				 params.protocol = 'TCP';
				 params.comment = 'testxxxxx';				 
				service.setPortForward(params,function(back){
					ok(back.result == 'success');										
						service.getPortForward({},function(result){							
							ok(result);	
								equals(result.portForwardRules.length,
										data.portForwardRules.length + 1,
										"portForwardRules number");

							params.portRange = params.portStart+' - '+params.portEnd;	
							delete params.portStart;
							delete params.portEnd;
							params.index = result.portForwardRules.length-1;
								validateObject(params,
										result.portForwardRules[result.portForwardRules.length - 1]);
							start();
						});					
				    });
			     }); 
			 
			 asyncTest('asyncTest deleteForwardRules', function() {
				 var data = service.getPortForward();
				 ok(data.portForwardRules.length > 0, 'portForwardRules exits: '+data.portForwardRules.length);
				 var params = {};
	             params.indexs = [data.portForwardRules.length-1];
				 service.deleteForwardRules(params,function(back){
					 ok(back.result == 'success');
					 service.getPortForward({},function(data1){
						 ok(data1.portForwardRules.length == data.portForwardRules.length-1,'delete ForwardRules success: '+data1.portForwardRules.length);						 
						 start();
					 });					 
				 });
			 });
			 
			 
			 asyncTest('asyncTest enableVirtualServer', function() {
				 var params = {};
				 params.portForwardEnable = '1';
				service.enableVirtualServer(params,function(back){
					 ok(back.result == 'success');
					 service.getPortForward({},function(data){
						 ok(data.portForwardEnable == '1','enable VirtualServer success');
						 start();
					 });					
				 });						 						 
			 });					 
										 						 			
			 asyncTest('setSdCardMode/getSDConfiguration', function() {
				service.getSDConfiguration({}, function(data) {
					var val = data.sd_mode == "0" ? '1' : '0';
					service.setSdCardMode({
						mode : val
					}, function(data) {
						log(data);
						ok(data.result, 'setSdCardMode succeed');
						service.getSDConfiguration({}, function(data) {
							ok(data.sd_mode == val, 'setSdCardMode succeed: ' + data.sd_mode);
							validateObject(data, {
								file_to_share : isIntegerString,
								sd_mode : isIntegerString,
								sd_status : isFullString,
								share_auth : isIntegerString,
								share_file : isString,
								share_status : isIntegerString,
								share_user : isString
							}, 'getSDConfiguration');
							val = val == "0" ? '1' : '0';
							service.setSdCardMode({
								mode : val
							}, function(data) {
								ok(data.result, 'setSdCardMode succeed');
								service.getSDConfiguration({}, function(data) {
									ok(data.sd_mode == val, 'setSdCardMode succeed: ' + data.sd_mode);
									validateObject(data, {
										file_to_share : isIntegerString,
										sd_mode : isIntegerString,
										sd_status : isFullString,
										share_auth : isIntegerString,
										share_file : isString,
										share_status : isIntegerString,
										share_user : isString
									}, 'getSDConfiguration');
									start();
								});
							});
						});
					});
				});
			});
			 
			 
			 
			 asyncTest('setSdCardSharing', function() {
					service.setSdCardSharing({
						share_status: '1',
						share_auth: '0',
						share_file: 'mmc2'
					},function(data){
						 ok(data.result == true,'setSdCardSharing succeed');
						 service.getSDConfiguration({},function(data){						 
						 ok(data.share_status == '1','share_status: '+data.share_status);	
						 ok(data.share_auth == '0','share_auth: '+data.share_auth);
						 ok(data.share_file == 'mmc2','share_file: '+data.share_file);
							 start();
						 });
				});					
			});
			 
			 			 
			 test('getPortFilter',function(){
				 var data = service.getPortFilter(); 
				 //log(data);
				   checkMatches(/0|1/, data.defaultPolicy);
				   checkMatches(/0|1/, data.portFilterEnable);
				   for ( var i = 0; i < data.portFilterRules.length; i++) {
						validateObject(data.portFilterRules[i], {
							action : isFullString,
							comment : isFullString,
							ipType : isString,
							destIpAddress : isIpAddressString,
							destPortRange : isFullString,							
							index : isInteger,
							protocol : isFullString,
							macAddress : isMacAddressString,
							sourceIpAddress : isIpAddressString,
							sourcePortRange : isFullString,
						}, 'check PortFilterRules');
					}
				 });
			 
			 
			 asyncTest('setPortFilter',function(){
				 var params = {};
				    params.macAddress = '00:DE:90:FF:FE:FC';
		            params.destIpAddress = '192.168.0.5';
		            params.sourceIpAddress = '192.168.1.5';
		            params.destPortStart = '1';
		            params.destPortEnd = '80';
		            params.sourcePortStart = '20';
		            params.sourcePortEnd = '90';
		            params.action = 'Drop';	
		            params.protocol = 'UDP';
		            params.comment = 'cc';
		            params.ipType = 'ipv4';
		            
				 service.setPortFilter(params,function(data){
					 ok(data.result == 'success');
					var data = service.getPortFilter();
					log(data);
					params.destPortRange = params.destPortStart +' - '+params.destPortEnd;
					params.sourcePortRange = params.sourcePortStart+' - '+params.sourcePortEnd;
					params.index = data.portFilterRules.length-1;                    
					switch(params.action){
					case 'Accept':{
						params.action = 'filter_accept';
						break;
					}
					case 'Drop':{
						params.action = 'filter_drop';
						break;
					}
					}
					delete params.destPortStart;
					delete params.destPortEnd;
					delete params.sourcePortStart;
					delete params.sourcePortEnd;
					params.ipType = params.ipType.toUpperCase();				
					data.portFilterRules[data.portFilterRules.length-1].ipType = data.portFilterRules[data.portFilterRules.length-1].ipType.toUpperCase();
					validateObject(data.portFilterRules[data.portFilterRules.length-1],params, 'setPortFilter succeed');					
					start();
				 });
			 });
			 
			 
			 asyncTest('setPortFilterBasic', function() {
				 var params = {};
				 params.portFilterEnable = '1';
				 params.defaultPolicy = '1';				 
					service.setPortFilterBasic(params,function(back){
						 ok(back.result == 'success' );
						 service.getPortFilter({},function(data){
						 ok(data.portFilterEnable == '1','portFilterEnable: '+data.portFilterEnable);	
						 ok(data.defaultPolicy == '1','defaultPolicy: '+data.defaultPolicy);
						start();
						 });
				});					
			});
			 
		asyncTest('deleteFilterRules', function() {
			var result = service.getPortFilter();
			ok(result.portFilterRules.length > 0, 'portFilterRules exists: '+result.portFilterRules.length);	
			     var param = {};
				 param.indexs = [String(result.portFilterRules.length-1)];			 
					service.deleteFilterRules(param,function(data){
						 ok(data.result == 'success','deleteFilterRules success');
						 service.getPortFilter({},function(result2){
						ok(result.portFilterRules.length - result2.portFilterRules.length == 1,'portFilterRules: '+result2.portFilterRules.length);
						start();
					});
				});					
			});
			 
		
		
		
		
		asyncTest('asyncTest set/get WifiRange',function(){
			var params = {
					wifiRangeMode: 'medium_mode'
			};
			service.setWifiRange(params,function(result1){				
				ok(result1.result == 'success');
				var data = service.getWifiRange();
				//log(data);
				validateObject(data,params,'set/get WifiRange');
				start();
			});
		});
		

		asyncTest('asyncTest set/get UpnpSetting',function(){
			var params = {};
			params.upnpSetting = '1';
			service.setUpnpSetting(params,function(result1){				
				ok(result1.result == 'success');
				var data = service.getUpnpSetting();
				//log(data);
				validateObject(data,params,'set/get UpnpSetting');
				start();
			});
		});
		
	
		
		asyncTest('asyncTest get/set DmzSetting',function(){
			var params = {};
			params.dmzSetting = "1";
			params.ipAddress = '192.168.0.100'; 
			service.setDmzSetting(params,function(result1){				
				ok(result1.result == 'success');
				var data = service.getDmzSetting();
				//log(data);
				validateObject(data,params,'get/set DmzSetting');
				start();
			});
		});
		
		
	
		
		asyncTest('asyncTest get/set QuickSettingInfo',function(){
	      var params = {
				APN_name : 'zte',
				Encryption_Mode_hid : 'WPAPSKWPA2PSK',
				Profile_Name : 'Vodafone',
				SSID_Broadcast : '1',
				SSID_name : 'wangle',
				WPA_PreShared_Key : '987654321',
				apn_mode :  'manual',
				ppp_auth_mode : 'manual',
				ppp_passwd :  'wwsa',
				ppp_username : 'dfsddd',
				security_shared_mode : 'chap'
		};
		service.setQuickSetting(params,function(result1){				
			ok(result1.result == 'success');
			var data = service.getQuickSettingInfo();
			//log(data);
			equals(params.Encryption_Mode_hid,data.AuthMode,'Auth_mode checked');
			equals(params.Profile_Name,data.m_profile_name,'profile_name checked');
			equals(params.SSID_Broadcast,data.HideSSID,'broadcast_mode checked');
			equals(params.SSID_name,data.SSID1,'ssid checked');			
			equals(params.WPA_PreShared_Key,data.WPAPSK1,'password checked');
			start();
		});
	});
		
		asyncTest('asyncTest getSdMemorySizes',function(){
			service.getSdMemorySizes({},function(data){	
					 if (data.result && data.result == "no_sdcard") {
					    ok(data.result,'"no_sdcard"');	
					} else {
						ok(isInteger(data.totalMemorySize));
						ok(isInteger(data.availableMemorySize) && data.availableMemorySize < data.totalMemorySize,'getSdMemorySize succeed');
					};
					start();
				}, function(error){
					if (isErrorObject(data)) {
						ok(false,'getSdMemorySizes failure');
						start();
						} 					
				});
		});
		
		
		
		asyncTest('asyncTest getFileList/fileRename',function(){
			var prePath = '';
			var basePath = '/mmc2';
			var path = '/';
			service.getFileList({
				path : prePath + basePath + path,
				index : 1
				},function(data){						
					 if (data.result == "no_sdcard") {
					    ok(data.result,'no_sdcard');	
					} else {
						ok(data,'getFileList succeed');
						var oldname = data.details[getRandomInt(parseInt(data.details.length)-1)].fileName;
						var oldPath = prePath + basePath + path + "/" + oldname;
						var newFolderName = oldname+'st';
						var newPath = prePath + basePath + path + "/" + newFolderName;						
						service.fileRename({
							oldPath : oldPath,
							newPath : newPath,
							path : prePath + basePath + path
							},function(result1){
								ok(result1.result = 'success');
								service.getFileList({
									path : prePath + basePath + path,
									index : 1},
								function(result2){
									var File = _.pluck(result2.details,'fileName');
									ok(_.include(File,newFolderName),'fileRename succeed');
									start();
								});
							});						
					}				
				}, function(error){
					if (isErrorObject(data)) {
						ok(false,'getFileList failure');
						start();
						} 					
				});
		});
			
	
		
		
		
		
		asyncTest('asyncTest createFolder/deleteFilesAndFolders',function(){
			var newPath = '/mmc2//wangle';
			service.createFolder({
				path : newPath},function(data){	
					 if (data.result && data.result == "no_sdcard") {
					    ok(data.result,'"no_sdcard"');} 
					 else if(data.result && data.result == "failure")
					    {ok(false,'create_folder_failure');}
					 else
						 {ok(data);
						    var prePath = '';
							var basePath = '/mmc2';
							var path = '/';
						 service.getFileList({							   
								path : prePath + basePath + path,
								index : 1
								},function(result){
									var File = _.pluck(result.details,'fileName');
									ok(_.include(File,'wangle'),'create_folder succeed');
									service.deleteFilesAndFolders({
										path : '/mmc2//',
										names : 'wangle*' 
									},function(result2){
										ok(result2);
									service.getFileList({
											 path : prePath + basePath + path,
											 index : 1 
										 },function(result3){
											 var File = _.pluck(result3.details,'fileName');
												ok(!_.include(File,'wangle*'),'deleteFilesAndFolders succeed');
												 start();
										 });																	
									});																							 
								});}
					           
				}, function(error){
					if (isErrorObject(data)) {
						ok(false,'createFolder failure');
						start();
						}}); 									
		});
		
		
		 asyncTest('asyncTest enablePortMap',function(){
			 var data = service.getPortMap();	  
			  ok(data.portMapEnable == '0');
			  params = {};	
			  params.portMapEnable = '1';
			  service.enablePortMap(params, function(result){
				  ok(result,result == 'success');
				  var data1 = service.getPortMap();
				  ok(data1.portMapEnable == '1','enable success');
				  params.portMapEnable = '0';
				  service.enablePortMap(params, function(result2){
					  ok(result2.result == 'success');
					  var data2 = service.getPortMap();
					  ok(data2.portMapEnable == '0','disable success');
					  start();
				  });				  
			    });				
			});
		
		
		
		 asyncTest('asyncTest setPortMap/getPortMap',function(){
			 var data1 = service.getPortMap();
			 var params = {};
			    params.portMapEnable = '1';
	            params.destIpAddress = '192.168.0.2';           	           
	            params.destPort = '90';
	            params.sourcePort = '20';	           
	            params.protocol = 'TCP';
	            params.comment = 'aa';
			 service.setPortMap(params,function(data){
				 ok(data.result == 'success');
				var data2 = service.getPortMap();
				if(data1.portMapRules.length < 10)
				{ok(data2.portMapRules.length - data1.portMapRules.length === 1,'add portMapRules');}
				if( params.protocol == 'TCP&UDP')
					{params.protocol = 'TCP+UDP'}
				delete data2.portMapRules[data2.portMapRules.length-1].index;
				equal(data2.portMapEnable,params.portMapEnable);
				delete(params.portMapEnable);
				validateObject(data2.portMapRules[data2.portMapRules.length-1],params, 'setPortMap succeed');							
				start();
			 });
		 });
						
		 asyncTest('asyncTest deleteMapRules',function(){
			 var data1 = service.getPortMap();
			 ok(data1.portMapRules.length >0,'MapRules exist');
			 var params = {};
			 params.indexs = [ String(getRandomInt(_.size(data1.portMapRules)-1)) ];	   
			 service.deleteMapRules(params,function(data){
				 ok(data.result == 'success');
				var data2 = service.getPortMap();
	           ok(data1.portMapRules.length - data2.portMapRules.length == 1,'deleteMapRules succeed');						
				start();
			 });
		 });
		
		 asyncTest('asyncTest set/get DlnaSetting',function(){
			 var params = {};
			 params.language = 'dlna test';
			 params.deviceName = 'english';
			 params.shareAudio = 'on';
			 params.shareImage = 'on';
			 params.shareVideo = 'on';  
			 service.setDlnaSetting(params,function(result){
			 ok(result.result == 'success','setDlnaSetting success');
			 data = service.getDlnaSetting();
			 delete data.dlnaEnable;
			 delete data.needRescan;
			 validateObject(data,params, 'getDlnaSetting success');
			 start();
			 });			 			
		 });
		 
		 asyncTest('asyncTest rescanDlna',function(){
			 var params = {};		 
			 service.rescanDlna(params, function(result) {
				 ok(result.result == "success");
			 start();
			 });			 			
		 });
		 
		 
		 
		/* asyncTest('asyncTest unlockNetwork/getNetworkUnlockTimes',function(){
			var status = service.getStatusInfo();
			if(status.simStatus == 'modem_imsi_waitnck')
			{
			 var params = {};	
			 params.unlock_network_code = 'aaaaffff12345678';
			 service.unlockNetwork(params, function(result) {
				 ok(result.result == "success");
			var data = service.getStatusInfo();
			ok(data.simStatus == 'modem_init_complete','unlockNetwork success');
			 start();
			 });	
			}
			else
				{var data = service.getNetworkUnlockTimes();
				ok(data);
				}
		 });*/
		 
		 
		/* asyncTest('asyncTest setUpdateInfoWarning/getUpdateInfoWarning',function(){
			 var params = {};
			 params.upgrade_notice_flag == 1;
			 service.setUpdateInfoWarning(params, function(result) {				 
			 ok(result);
			 var notice=service.getUpdateInfoWarning();
			 log(notice);
			 start();
			 });			 			
		 });*/
		 
		 
		 asyncTest('asyncTest get/set APStationBasic',function(){
			 var params = {};
			 params.ap_station_enable = 1;
			 params.ap_station_mode = 1;
			 service.setAPStationBasic(params, function(result) {				 
			 ok(result);
			 var data=service.getAPStationBasic();
			 equals(data.ap_station_enable,params.ap_station_enable);
			 equals(data.ap_station_mode,params.ap_station_mode);
			 start();
			 });			 			
		 });
		 
		 test('getHotspotList', function() {
				var data = service.getHotspotList();
				log(data);
				if(data.hotspotList.length >0)
				{
				checkMatches(/OPEN|SHARED|WPA|WPA2|WPAWPA2/, data.hotspotList[data.hotspotList.length-1].authMode);				
				checkMatches(/[0-1]/, data.hotspotList[data.hotspotList.length-1].connectStatus);				
				checkMatches(/WEP|TKIP|AES/, data.hotspotList[data.hotspotList.length-1].encryptType);				
				checkMatches(/[0-9]/, data.hotspotList[data.hotspotList.length-1].fromProvider);				
				checkMatches(/[0-3]/, data.hotspotList[data.hotspotList.length-1].keyID);
				checkMatches(/^[0-9a-zA-Z][\s._\-0-9a-zA-Z]{7,63}$/, data.hotspotList[data.hotspotList.length-1].password);				
				checkMatches(/^[0-9a-zA-Z][\s._\-0-9a-zA-Z]{0,31}$/, data.hotspotList[data.hotspotList.length-1].profileName);				
				checkMatches(/[0-5]/, data.hotspotList[data.hotspotList.length-1].signal);
				checkMatches(/^[0-9a-zA-Z][\s._\-0-9a-zA-Z]{0,31}$/, data.hotspotList[data.hotspotList.length-1].ssid);
				}
				else
				{ok(data.hotspotList.length == 0);}
			});
		 
		
		 function search() {
			 var result = service.getSearchHotspotList();
			 if (result.scan_finish) {
			
			 } else {
			 setTimeout(search, 1000);
			 }
		 }
		 
		 
		 
		 
		 asyncTest('asyncTest searchHotspot/getSearchHotspotList/saveHotspot/deleteHotspot',function(){
			 service.searchHotspot({}, function (result) {
				if (result && result.result == "success")
				{   search();
					var data = service.getSearchHotspotList();
				   // log(data);
				    params = {
				    		    authMode: data.hotspotList[0].authMode,
				    	        encryptType: data.hotspotList[0].encryptType,
				    	    	keyID: "0",
				    	    	password: "123456789",
				    	    	profileName: "",
				    	    	signal: data.hotspotList[0].signal,
				    	    	ssid: data.hotspotList[0].ssid
				    };
				    var data0 = service.getHotspotList();	
				    var oldNumber = data0.hotspotList.length;
				    log(data0.hotspotList.length);
				   params.apList = data0.hotspotList;
				   /* for(i=0;i<params.aplist.length;i++)
				    {}
				    params.aplist[i].imgSignal = 'img/wifi_signal_3.png';*/
				    service.saveHotspot(params, function(result2){
				      ok(result2);
				      para = {};
				    var data1 = service.getHotspotList();
				    //log(data1.hotspotList[0].profileName);
				    log(data1.hotspotList.length);
				    ok(data1.hotspotList.length == oldNumber + 1, 'save hotspot success');
				    para.profileName = data1.hotspotList[data1.hotspotList.length-1].profileName;
				    para.apList = data1.hotspotList;
				    service.deleteHotspot(para, function (data2) {
				    ok(data2);	
				    var data2 = service.getHotspotList();
				    ok(data1.hotspotList.length - data2.hotspotList.length == 1, 'delete hotspot success');
				    start();
				      });
				    });				  				   
				}else{
				ok(result,'ap_station_search_hotspot_fail');
				start();
				}				 
			 });			 
		 });
		 
		 
		 
		 
		 
		 asyncTest('asyncTest set/get TrafficAlertInfo',function(){
			 var params = {
					 dataLimitChecked: '0',
					 dataLimitTypeChecked: '1',
					 limitDataMonth: '100_1',
					 alertDataReach: '80',
					 limitTimeMonth: '0',
					 alertTimeReach: '0'
			 };
			 service.setTrafficAlertInfo(params,function(result){
				 ok(result.result == 'success');
				 var data = service.getTrafficAlertInfo();
				 log(data);
				 validateObject(data, params);
				 start();
			 });
		 });
		 
		 asyncTest('asyncTest set/get FastbootSetting',function(){
			 var params = {};
			 params.fastbootEnabled = 1;
			 service.setFastbootSetting(params, function(result) {
			 ok(result);			
		     var data = service.getFastbootSetting();
		     //log(data);
		     equals(data.fastbootEnabled,params.fastbootEnabled,'set FastbootSetting success');
		     params.fastbootEnabled = 0;
		     service.setFastbootSetting(params, function(result1) {
		     ok(result1);
		      start();
		     });		     
		   });
		 });
		 
		 asyncTest('asyncTest setQuickSetting4IPv6',function(){
			 var params = {
					 apn_index:'2',
		             pdp_type:'IP',
		             apnMode:'internet.ChinaMobile.gr',
		             profile_name:'ChinaMobile',
		             wan_apn:'internet.ChinaMobile.gr',
		             ppp_auth_mode:'2',
		             ppp_username:'2',
		             ppp_passwd:'2',
		             ipv6_wan_apn:'2',
		             ipv6_ppp_auth_mode:'pap',
		             ipv6_ppp_username:'edddkk',
		             ipv6_ppp_passwd:'235434',
		             SSID_name:'43543',
		             SSID_Broadcast:'0',
		             Encryption_Mode_hid: 0,
		             security_shared_mode:'NONE',
		             WPA_PreShared_Key:'12345678',
		             wep_default_key: 0,
		             WPA_ENCRYPTION_hid: 0
			 }; 
			 service.setQuickSetting4IPv6(params, function(result){
				 ok(result.result == 'success');
				 var data = service.getQuickSettingInfo();
				 //log(data);
				 equal(data.m_profile_name, params.profile_name,'check profile_name');
				 //equal(data.AuthMode, params.ipv6_ppp_auth_mode,'check AuthMode');
				 equal(data.HideSSID, params.SSID_Broadcast,'check SSID_Broadcast');
				 //equal(data.RadioOff, params.profile_name,'check profile_name');				 				 
				 equal(data.WPAPSK1, params.WPA_PreShared_Key,'check WPA_PreShared_Key');
				 equal(data.SSID1, params.SSID_name,'check SSID_name');				 
				 equal(data.apn_mode, params.apnMode,'check apnMode');
				 equal(data.pdp_type, params.pdp_type,'check pdp_type');				 
				 start();
			 });
		 });
		 
		 
		 
		 
			 
			}());
		});