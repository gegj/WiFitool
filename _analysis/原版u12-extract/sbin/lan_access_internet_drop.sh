#!/bin/sh
#
# $Id: lan_access_internet_drop.sh
#
lan_access_internet_by_flag=`nv get lan_access_internet_by_flag`
if [ "$lan_access_internet_by_flag" != "1" ];then
	echo "NV lan_access_internet_by_flag is disable"
	exit
fi
wan_v4_dev_name=`nv get wan_v4_dev_name`
if [ "$wan_v4_dev_name" == "" ];then
	wan_v4_dev_name="wan1"
fi
zhongyuan_disable_connect_flag=`nv get zhongyuan_disable_connect_flag`
echo "zhongyuan_disable_connect_flag=$zhongyuan_disable_connect_flag"

ret=`iptables -t filter -nvxL lan_access_internet_drop`
if [ "$ret" == "" -o "$ret" == "iptables: No chain/target/match by that name." ] ; then
	iptables -t filter -N lan_access_internet_drop
    ip6tables -t filter -N lan_access_internet_drop
fi
ret=`iptables -t filter -nvxL FORWARD | grep lan_access_internet_drop`
if [ "$ret" == "" -o "$ret" == "iptables: No chain/target/match by that name." ] ; then			
	iptables -t filter -I FORWARD 1 -j lan_access_internet_drop
    ip6tables -t filter -I FORWARD 1 -j lan_access_internet_drop
fi	

iptables -t filter -F lan_access_internet_drop
ip6tables -t filter -F lan_access_internet_drop

if [ "$zhongyuan_disable_connect_flag" == "1" ];then
   #disable connect to internet;
    iptables -A lan_access_internet_drop -i $wan_v4_dev_name  -j DROP
    ip6tables -A lan_access_internet_drop -i $wan_v4_dev_name  -j DROP
    echo 0 > /proc/net/fastnat_level
else
    echo 2 > /proc/net/fastnat_level
fi	