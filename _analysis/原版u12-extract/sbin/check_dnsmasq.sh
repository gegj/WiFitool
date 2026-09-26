#!/bin/sh
# author: router 
# date: 20210108

index="1"
while [ "$index" -lt 30 ]
do
	ret=`ps | grep -w /etc_rw/dnsmasq.conf | grep -v grep`
	if [ "$ret" == "" ] ; then
		dnsmasq -i br0 -r /etc_rw/dnsmasq.conf &
	fi
	index=`expr $index + 1`
	sleep 2
done
