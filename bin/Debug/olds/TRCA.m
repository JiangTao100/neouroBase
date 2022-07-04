function [result,ansm]=TRCA(FreqNum,data,BandUsed)
StartTime=0;
DataLength=1;
srate=1000;
load('Temple.mat');
result=[];
ansm=0;
for iBand=1:10
    [b{iBand},a{iBand}] = cheby1(8,1,[(8*iBand-2)*2/srate],'high');
end
[b7,a7] = cheby1(8,1,7*2/srate,'high');
[b90,a90] = cheby1(8,1,90*2/srate,'low');

data=filtfilt(b7,a7,double(data));
data=filtfilt(b90,a90,double(data));

for iBand=1:10  
    TestSet{1,iBand}=filtfilt(b{iBand},a{iBand} ,data);    
end

%% TRCA
A=(1:10).^-1.25+0.25;
r=[];
for iFreq=1:FreqNum
    for iBand=1:10
        r(iFreq,iBand)=corr2(TestSet{1,iBand}(StartTime+1:StartTime+DataLength*srate,:)*W{iBand},...
            T{iFreq,iBand}(StartTime+1:StartTime+DataLength*srate,:)*W{iBand});
    end
    ansm(iFreq)=sum(A(BandUsed).*r(iFreq,BandUsed));
    [~,result]=max(ansm);
end
result=T{1}(1,1);

