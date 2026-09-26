#!/bin/sh
# author: router 
# date: 20210108

log_enable=`nv get dns_redirect_log_enabled`
LOG_FILE="/tmp/redirect.log"
log() {
	if [ "$log_enable" != "0" ] ; then
		echo "[$(date '+%Y-%m-%d %H:%M:%S')] [firewall_dns_redirect.sh] $1" >> $LOG_FILE
	fi	
}
log "run firewall_dns_redirect.sh "

lan_dns_redirect_enabled=`nv get lan_dns_redirect_enabled`
log "lan_dns_redirect_enabled=$lan_dns_redirect_enabled"
second_auth_level=`nv get seecom_card_flag_level`
log "second_auth_level=$second_auth_level"
second_auth_state=`nv get second_auth_state`
log "second_auth_state=$second_auth_state"

if [ "$lan_dns_redirect_enabled" != "1" ] ; then
	return
fi 

ret=`iptables -t filter -nvxL authed_mac`
if [ "$ret" == "" ] ; then
	iptables -t filter -N authed_mac
	log "iptables -t filter -N authed_mac"
fi

ret=`iptables -t filter -nvxL FORWARD | grep authed_mac`
if [ "$ret" == "" -o "$ret" == "iptables: No chain/target/match by that name." ] ; then			
	iptables -t filter -I FORWARD 1 -j authed_mac
	log "iptables -t filter -I FORWARD 1 -j authed_mac"
fi

ret=`ip6tables -t filter -nvxL authed_mac`
if [ "$ret" == "" ] ; then
	ip6tables -t filter -N authed_mac
	log "ip6tables -t filter -N authed_mac"
fi

ret=`ip6tables -t filter -nvxL FORWARD | grep authed_mac`
if [ "$ret" == "" -o "$ret" == "ip6tables: No chain/target/match by that name." ] ; then			
	ip6tables -t filter -I FORWARD 1 -j authed_mac
	log "ip6tables -t filter -I FORWARD 1 -j authed_mac"
fi

ret=`iptables -t nat -nvxL dns_redirect`
if [ "$ret" == "" ] ; then
	iptables -t nat -N dns_redirect
	log "iptables -t nat -N dns_redirect"
fi

ret=`iptables -t nat -nvxL PREROUTING | grep dns_redirect`
if [ "$ret" == "" -o "$ret" == "iptables: No chain/target/match by that name." ] ; then			
	iptables -t nat -I PREROUTING 1 -j dns_redirect
	log "iptables -t nat -I PREROUTING 1 -j dns_redirect"
fi

iptables -t filter -F authed_mac
iptables -t nat -F dns_redirect
ip6tables -t filter -F authed_mac
log "iptables -t filter -F authed_mac"
log "iptables -t nat -F dns_redirect"
log "ip6tables -t filter -F authed_mac"

seecom_card_flag=`nv get seecom_card_flag`
log "seecom_card_flag=$seecom_card_flag"
if [ "$seecom_card_flag" == "0" -o "$seecom_card_flag" == "" ] ; then
	log "seecom_card_flag is 0,exit"
	exit
fi
if [ "$second_auth_level" = "1" ] && [ "$second_auth_state" = "1" ]; then
    log "clean iptables rules"
	exit
fi
lan_ipaddr=`nv get lan_ipaddr`
iptables -t nat -A dns_redirect -i br0 -j DNAT -p udp --dport 53 --to ${lan_ipaddr}:53
iptables -t nat -A dns_redirect -i br0 -j DNAT -p tcp --dport 53 --to ${lan_ipaddr}:53
log "iptables -t nat -A dns_redirect -i br0 -j DNAT -p udp --dport 53 --to ${lan_ipaddr}:53"
log "iptables -t nat -A dns_redirect -i br0 -j DNAT -p tcp --dport 53 --to ${lan_ipaddr}:53"

default_wan_name=`nv get default_wan_name`

routerH5Url=`nv get routerH5Url`
domain_port=`echo $routerH5Url | cut -d '/' -f 3`
domain=`echo $domain_port | cut -d ':' -f 1`

log "domain=[$domain]"
nv set routerH5Domain=$domain

seecom_card_carrier_type=`nv get seecom_card_carrier_type`
if [ "$seecom_card_carrier_type" == "YD" ] ; then
	redirectTo_ip_port=`echo $routerH5Url | awk -F "redirectTo=" '{print $2}' | cut -d '/' -f 3`
elif [ "$seecom_card_carrier_type" == "DX" ] ; then
	redirectTo_ip_port=`echo $routerH5Url | awk -F "servAddr=" '{print $2}' | cut -d '/' -f 3`
fi

redirectTo_ip=`echo $redirectTo_ip_port | cut -d ':' -f 1`

wan1_pridns=`nv get wan1_pridns`
if [ "$wan1_pridns" == "" ] ; then
	wan1_pridns="8.8.8.8"
fi

ip_type=$(router_msg_proxy ipcheck $redirectTo_ip)
log "redirectTo_ip=[$redirectTo_ip] is ip_type[$ip_type]"
if [ "$ip_type" == "4" ] ; then
	iptables -t filter -I authed_mac -d $redirectTo_ip -j ACCEPT
	log "iptables -t filter -I authed_mac -d $redirectTo_ip -j ACCEPT"
elif [ "$ip_type" == "6" ] ; then
	ip6tables -t filter -I authed_mac -d $redirectTo_ip -j ACCEPT
	log "ip6tables -t filter -I authed_mac -d $redirectTo_ip -j ACCEPT"
else
	redirectTo_domain=$redirectTo_ip
	nv set routerH5RedirectDomain=$redirectTo_domain
	LocalDomain=`nv get LocalDomain`
	if [ "$LocalDomain" != "$redirectTo_domain" ] ; then
		nslookup $redirectTo_domain $wan1_pridns > /tmp/domain_ip.txt
		index="1"
		ipaddr=`cat /tmp/domain_ip.txt | tail -n +5 | cut -d  ' ' -f 3 | sed -n "${index}p"`
		while [ "$ipaddr" != "" ]
		do
			ip_type=$(router_msg_proxy ipcheck $ipaddr)
			log "ipaddr=[$ipaddr] is ip_type[$ip_type]"
			if [ "$ip_type" == "4" ] ; then
				iptables -t filter -I authed_mac -d $ipaddr -j ACCEPT
				log "iptables -t filter -I authed_mac -d $ipaddr -j ACCEPT"
			elif [ "$ip_type" == "6" ] ; then
				ip6tables -t filter -I authed_mac -d $ipaddr -j ACCEPT
				log "ip6tables -t filter -I authed_mac -d $ipaddr -j ACCEPT"
			fi
			index=`expr $index + 1`
			ipaddr=`cat /tmp/domain_ip.txt | tail -n +5 | cut -d  ' ' -f 3 | sed -n "${index}p"`
		done
	fi
fi


#nslookup $domain $wan1_pridns > /tmp/domain_ip.txt

output=$(nslookup "$domain" "$wan1_pridns" 2>/dev/null)
log "nslookup output=[$output]"
if echo "$output" | grep -q "^Name:" && echo "$output" | grep -q "^Address [0-9]"; then
    echo "$output" > /tmp/domain_ip.txt
fi
index="1"
ipaddr=`cat /tmp/domain_ip.txt | tail -n +5 | cut -d  ' ' -f 3 | sed -n "${index}p"`
log "ipaddr=[$ipaddr]"
while [ "$ipaddr" != "" ]
do
	ip_type=$(router_msg_proxy ipcheck $ipaddr)
	log "ipaddr=[$ipaddr] is ip_type[$ip_type]"
	if [ "$ip_type" == "4" ] ; then
		iptables -t filter -I authed_mac -d $ipaddr -j ACCEPT
		log "iptables -t filter -I authed_mac -d $ipaddr -j ACCEPT"
	elif [ "$ip_type" == "6" ] ; then
		ip6tables -t filter -I authed_mac -d $ipaddr -j ACCEPT
		log "ip6tables -t filter -I authed_mac -d $ipaddr -j ACCEPT"
	fi
	index=`expr $index + 1`
	ipaddr=`cat /tmp/domain_ip.txt | tail -n +5 | cut -d  ' ' -f 3 | sed -n "${index}p"`
done


auth_mac_list=`nv get auth_mac_list`
index="1"
auth_mac_time=`echo $auth_mac_list | awk -v i="$index" -F ';' '{print $i}'`
while [ "$auth_mac_time" != "" ]
do
	if [ "$auth_mac_time" != "" ] ; then
		auth_mac=`echo $auth_mac_time | awk -F ',' '{print $1}'`
		iptables -t filter -I authed_mac -m mac --mac-source $auth_mac -j ACCEPT
		ip6tables -t filter -I authed_mac -m mac --mac-source $auth_mac -j ACCEPT
		log "iptables -t filter -I authed_mac -m mac --mac-source $auth_mac -j ACCEPT"
		log "ip6tables -t filter -I authed_mac -m mac --mac-source $auth_mac -j ACCEPT"
	fi
	index=`expr $index + 1`
	auth_mac_time=`echo $auth_mac_list | awk -v i="$index" -F ';' '{print $i}'`
done
iptables -t filter -A authed_mac -o $default_wan_name  -j DROP
ip6tables -t filter -A authed_mac -o $default_wan_name  -j DROP
log "iptables -t filter -A authed_mac -o $default_wan_name  -j DROP"
log "ip6tables -t filter -A authed_mac -o $default_wan_name  -j DROP"