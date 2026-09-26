#!/bin/sh
# ===========================================================
# usage: tc_control.sh
# traffic control by tc_uplink and tc_downlink

path_sh=`nv get path_sh`
. $path_sh/global.sh

echo "Info: tc_tbf $1 $2 start "
echo "Info: tc_tbf $1 $2 start" >> $test_log

#add by zte router speedlimit start
#流控上下行阀值，为空或为0表示不进行流控，暂时只实现上行的tc，下行将来根据实际需要再扩展实现
UPLINK=`nv get tc_uplink`
DOWNLINK=`nv get tc_downlink`
def_cid=`nv get default_cid`
tc_enable=`nv get tc_enable`
sim1_status=`nv get sim1_status`
sim2_status=`nv get sim2_status`
sim3_status=`nv get sim3_status`

#tc_enable=0，流量控制功能关闭，直接退出
if [ "$tc_enable" == "0" ] ; then
    if [ "$sim1_status" == "1" -a "$sim2_status" == "1" -a "$sim3_status" == "1" ] || [ "$sim1_status" == "" -a "$sim2_status" == "" -a "$sim3_status" == "" ] ; then
		echo "tc_enable=0" 
		echo "tc_enable=0" >> $test_log
		
		#modify by zte router close speedlimit clear tc rule begin
		IN=`nv get lan_name`
		OUT4=$defwan_rel
		OUT6=$defwan6_rel
		tc qdisc del dev $IN root
		tc qdisc del dev $OUT4 root
		tc qdisc del dev $OUT6 root
		#modify by zte router close speedlimit clear tc rule end
		
		#给内核恢复快速转发级别
		fastnat_level=`nv get fastnat_level`
		echo "Info: fastnat_level restore to:$fastnat_level" >> $test_log
		echo $fastnat_level > /proc/net/fastnat_level
		exit 0
	fi
fi


if [ "$DOWNLINK" == "" ]; then
	DOWNLINK=0
fi
speed1=`nv get sim1_limit_speed`
speed2=`nv get sim2_limit_speed`
speed3=`nv get sim3_limit_speed`
sim1_limit_speed=$(expr $speed1 \* 1024 / 8 / 2)
sim2_limit_speed=$(expr $speed2 \* 1024 / 8 / 2)
sim3_limit_speed=$(expr $speed3 \* 1024 / 8 / 2)
simcard_active_slot_temp=`nv get simcard_active_slot_temp`

if [ "$simcard_active_slot_temp" == "1" ]; then
    if [ "$sim1_status" == "0" ]; then
        if [ "$tc_enable" == "0" ]; then
            DOWNLINK=$sim1_limit_speed
            UPLINK=$sim1_limit_speed
        fi
		if [ "$tc_enable" == "1" ]; then
			if [ "$DOWNLINK" == "0" ] || [ "$sim1_limit_speed" -lt "$DOWNLINK" ]; then
				DOWNLINK=$sim1_limit_speed
				UPLINK=$sim1_limit_speed
			fi
        fi
    fi
fi
if [ "$simcard_active_slot_temp" == "2" ]; then
    if [ "$sim2_status" == "0" ]; then
		if [ "$tc_enable" == "0" ]; then
            DOWNLINK=$sim2_limit_speed
            UPLINK=$sim2_limit_speed
        fi
		if [ "$tc_enable" == "1" ]; then
			if [ "$DOWNLINK" == "0" ] || [ "$sim2_limit_speed" -lt "$DOWNLINK" ]; then
				DOWNLINK=$sim2_limit_speed
				UPLINK=$sim2_limit_speed
			fi
        fi
    fi
fi
if [ "$simcard_active_slot_temp" == "3" ]; then
    if [ "$sim3_status" == "0" ]; then
		if [ "$tc_enable" == "0" ]; then
            DOWNLINK=$sim3_limit_speed
            UPLINK=$sim3_limit_speed
        fi
		if [ "$tc_enable" == "1" ]; then
			if [ "$DOWNLINK" == "0" ] || [ "$sim3_limit_speed" -lt "$DOWNLINK" ]; then
				DOWNLINK=$sim3_limit_speed
				UPLINK=$sim3_limit_speed
			fi
        fi
    fi
fi
#add by zte router speedlimit end

need_jilian=`nv get need_jilian`
lanEnable=`nv get LanEnable`
if [ "$need_jilian" == "1" ]; then
    if [ "$lanEnable" == "1" ]; then
        IN=`nv get lan_name`
    elif [ "$lanEnable" == "0" ]; then
        IN=`nv get "ps_ext"$def_cid`
    fi
elif [ "$need_jilian" == "0" ]; then
    IN=`nv get lan_name`
fi

#双栈时，ipv4和ipv6的默认外网口可能不一致，虽然短期内都不会有实际场景
OUT4=$defwan_rel
OUT6=$defwan6_rel

if [ "$lanEnable" == "1" ]; then
    GATEWAY=`nv get lan_ipaddr`
fi

echo "IN=$IN, OUT4=$OUT4, OUT6=$OUT6, GATEWAY=$GATEWAY, DOWNLINK=$DOWNLINK, UPLINK=$UPLINK"
echo "IN=$IN, OUT4=$OUT4, OUT6=$OUT6, GATEWAY=$GATEWAY, DOWNLINK=$DOWNLINK, UPLINK=$UPLINK" >> $test_log

#清空原先的流程规则
tc qdisc del dev $IN root
if [ "$OUT4" != "" ]; then
    tc qdisc del dev $OUT4 root
fi
if [ "$OUT6" != "" -a "$OUT6" != "$OUT4" ]; then
    echo "clear tc for $OUT6"
    tc qdisc del dev $OUT6 root
fi

#给内核恢复快速转发级别
fastnat_level=`nv get fastnat_level`
echo "Info: fastnat_level restore to：$fastnat_level" >> $test_log
echo $fastnat_level > /proc/net/fastnat_level

ifconfig $IN txqueuelen 10
if [ "$OUT4" != "" ]; then
    ifconfig $OUT4 txqueuelen 10
fi
if [ "$OUT6" != "" -a "$OUT6" != "$OUT4" ]; then
    ifconfig $OUT6 txqueuelen 10
fi

#适配之前的客户：如果$1不等于down/DOWN，就按up/UP处理
if [ "$1" == "down" -o "$1" == "DOWN" ]; then
	echo "traffic control down" 
	echo "traffic control down" >> $test_log
	exit 0
fi

if [ "$DOWNLINK" == "" -o "$DOWNLINK" == "0" ] && [ "$UPLINK" == "" -o "$UPLINK" == "0" ]; then
    echo "no need to traffic control"
    echo "no need to traffic control" >> $test_log
    exit 0
fi

#暂定uc/v2都需要关闭快速转发

echo 0 > /proc/net/fastnat_level

if [ "$DOWNLINK" != "0" -a "$DOWNLINK" != "" ]; then
    echo "traffic control for down"
    echo "traffic control for down" >> $test_log
    
    LOCAL=`nv get tc_local`
    SUM=`expr ${DOWNLINK} + ${LOCAL}`
    echo "LOCAL=$LOCAL, SUM=$SUM"
    echo "LOCAL=$LOCAL, SUM=$SUM" >> $test_log
    
    ifconfig $IN txqueuelen 1000

    #限速的大小单位虽然是bps，但实际是字节
    tc qdisc add dev $IN root handle 1: htb default 20
    tc class add dev $IN parent 1: classid 1:1 htb rate ${SUM}bps
    tc class add dev $IN parent 1:1 classid 1:20 htb rate ${DOWNLINK}bps
    tc class add dev $IN parent 1:1 classid 1:10 htb rate ${LOCAL}bps
    tc qdisc add dev $IN parent 1:10 handle 10: sfq perturb 10
    tc qdisc add dev $IN parent 1:20 handle 20: sfq perturb 10
    tc filter add dev $IN protocol ip parent 1:0 prio 1 u32 match ip src ${GATEWAY}/32 match ip sport 80 0xffff flowid 1:10
fi

if [ "$UPLINK" != "0" -a "$UPLINK" != "" ]; then
    if [ "$OUT4" != "" ]; then
        echo "traffic control for up - ipv4"
        echo "traffic control for up - ipv4" >> $test_log
        ifconfig $OUT4 txqueuelen 1000
        tc qdisc add dev $OUT4 root handle 1: htb default 1
        tc class add dev $OUT4 parent 1: classid 1:1 htb rate ${UPLINK}bps
    fi

    if [ "$OUT6" != "" -a "$OUT6" != "$OUT4" ]; then
        echo "traffic control for up - ipv6"
        echo "traffic control for up - ipv6" >> $test_log
        ifconfig $OUT6 txqueuelen 1000
        tc qdisc add dev $OUT6 root handle 1: htb default 1
        tc class add dev $OUT6 parent 1: classid 1:1 htb rate ${UPLINK}bps
    fi
fi