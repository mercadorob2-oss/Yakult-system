package com.example.yakultscanner.utils

import android.content.Context
import android.graphics.Canvas
import android.graphics.Color
import android.graphics.Paint
import android.graphics.RectF
import android.graphics.pdf.PdfDocument
import com.example.yakultscanner.api.CallTicketListItem
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

fun generateCallMonitoringPdf(context: Context, tickets: List<CallTicketListItem>, tabTitle: String, searchQuery: String, totalCount: Int, page: Int, pageSize: Int) {
    val doc = PdfDocument()
    val pw = 595; val ph = 842
    val ml = 32f; val cr = pw - ml
    val genAt = SimpleDateFormat("MMM dd, yyyy hh:mm a", Locale.US).format(Date())
    val red = Color.rgb(189,36,45); val rd = Color.rgb(123,24,30); val ink = Color.rgb(32,37,41); val mu = Color.rgb(98,108,119); val sb = Color.rgb(221,226,230)
    val bp = Paint(Paint.ANTI_ALIAS_FLAG).apply{color=red}; val sfp = Paint(Paint.ANTI_ALIAS_FLAG).apply{color=Color.argb(44,255,255,255)}
    val tp = Paint(Paint.ANTI_ALIAS_FLAG).apply{color=Color.WHITE;textSize=20f;isFakeBoldText=true}
    val blp = Paint(Paint.ANTI_ALIAS_FLAG).apply{color=Color.argb(200,255,255,255);textSize=9f;isFakeBoldText=true}
    val bvp = Paint(Paint.ANTI_ALIAS_FLAG).apply{color=Color.WHITE;textSize=12f;isFakeBoldText=true}
    val st = Paint(Paint.ANTI_ALIAS_FLAG).apply{color=Color.WHITE;textSize=24f;isFakeBoldText=true;textAlign=Paint.Align.CENTER}
    val hfp = Paint(Paint.ANTI_ALIAS_FLAG).apply{color=Color.rgb(255,235,238)}; val hpp = Paint(Paint.ANTI_ALIAS_FLAG).apply{color=rd;textSize=7.8f;isFakeBoldText=true}
    val cp = Paint(Paint.ANTI_ALIAS_FLAG).apply{color=ink;textSize=8.2f}; val mp = Paint(Paint.ANTI_ALIAS_FLAG).apply{color=mu;textSize=7.8f}
    val fp = Paint(Paint.ANTI_ALIAS_FLAG).apply{color=mu;textSize=8f}; val dp = Paint(Paint.ANTI_ALIAS_FLAG).apply{color=sb;strokeWidth=1f}
    val bop = Paint(Paint.ANTI_ALIAS_FLAG).apply{color=sb;style=Paint.Style.STROKE;strokeWidth=1f}
    val arp = Paint(Paint.ANTI_ALIAS_FLAG).apply{color=Color.rgb(252,252,252)}; val wrp = Paint(Paint.ANTI_ALIAS_FLAG).apply{color=Color.WHITE}
    fun safe(v:String?,fb:String="—")=v?.trim()?.takeIf{it.isNotEmpty()}?:fb
    fun ell(t:String,p:Paint,mw:Float):String{val tr=t.trim();if(p.measureText(tr)<=mw)return tr;val e="...";var lo=0;var hi=tr.length;while(lo<hi){val md=(lo+hi)/2;if(p.measureText(tr.substring(0,md)+e)<=mw)lo=md+1 else hi=md};val en=(lo-1).coerceAtLeast(0);return if(en==0)e else tr.substring(0,en)+e}
    fun sbg(s:String?):Paint{val c=when(s?.lowercase(Locale.US)){"pending","reopened"->Color.rgb(255,243,224);"solved"->Color.rgb(232,245,233);"escalated"->Color.rgb(243,229,245);else->Color.rgb(245,245,245)};return Paint(Paint.ANTI_ALIAS_FLAG).apply{color=c;style=Paint.Style.FILL}}
    fun stx(s:String?):Paint{val c=when(s?.lowercase(Locale.US)){"pending","reopened"->Color.rgb(230,81,0);"solved"->Color.rgb(27,94,32);"escalated"->Color.rgb(106,27,154);else->Color.rgb(98,108,119)};return Paint(Paint.ANTI_ALIAS_FLAG).apply{color=c;textSize=7f;isFakeBoldText=true}}
    var pn=1;fun np():Pair<PdfDocument.Page,Canvas>{val i=PdfDocument.PageInfo.Builder(pw,ph,pn++).create();val p=doc.startPage(i);return p to p.canvas}
    data class PS(val p:PdfDocument.Page,val c:Canvas,var y:Float,val n:Int)
    fun db(c:Canvas):Float{val br=RectF(ml,28f,cr,110f);c.drawRoundRect(br,20f,20f,bp);val sr=RectF(ml+18f,42f,ml+68f,92f);c.drawOval(sr,sfp);c.drawText("Y",sr.centerX(),77f,st);val tl=sr.right+14f;c.drawText("YAKULT PHILIPPINES, INC.",tl,53f,blp);c.drawText("IT Call Monitoring — $tabTitle",tl,76f,tp);val pr=RectF(tl,84f,tl+100f,99f);c.drawRoundRect(pr,8f,8f,sfp);val pt=Paint(Paint.ANTI_ALIAS_FLAG).apply{color=Color.WHITE;textSize=8f;isFakeBoldText=true};c.drawText("EXPORT COPY",tl+8f,95f,pt);val rx=cr-155f;c.drawText("TOTAL TICKETS",rx,53f,blp);c.drawText("$totalCount tickets",rx,72f,bvp);c.drawText("GENERATED",rx,88f,blp);c.drawText(ell(genAt,bvp,145f),rx,104f,bvp);return br.bottom+14f}
    fun df(c:Canvas,y:Float):Float{val fl=mutableListOf<String>();if(searchQuery.isNotBlank())fl.add("Search: \"$searchQuery\"");if(tabTitle.isNotBlank())fl.add("Tab: $tabTitle");val ft=if(fl.isEmpty())"Showing all tickets" else fl.joinToString("  •  ");val fw=cp.measureText(ft)+16f;val fr=RectF(ml,y,ml+fw,y+18f);c.drawRoundRect(fr,8f,8f,Paint(Paint.ANTI_ALIAS_FLAG).apply{color=Color.rgb(255,235,238);style=Paint.Style.FILL});c.drawRoundRect(fr,8f,8f,Paint(Paint.ANTI_ALIAS_FLAG).apply{color=Color.rgb(255,205,210);style=Paint.Style.STROKE;strokeWidth=1f});c.drawText(ft,ml+8f,y+12.5f,Paint(Paint.ANTI_ALIAS_FLAG).apply{color=Color.rgb(189,36,45);textSize=8f});return y+26f}
    fun fpp(pp:PdfDocument.Page,cc:Canvas,n:Int){val fy=ph-24f;cc.drawLine(ml,fy-10f,cr,fy-10f,dp);cc.drawText("Yakult Philippines, Inc. — IT Call Monitoring",ml,fy,fp);cc.drawText("Page $n",cr-42f,fy,fp);doc.finishPage(pp)}
    fun npp():PS{val n=pn-1;val(pp,cc)=np();var y=db(cc);y=df(cc,y);return PS(pp,cc,y,n+1)}
    val c1=ml+8f;val c2=ml+92f;val c3=ml+242f;val c4=ml+346f;val c5=ml+402f;val c6=ml+456f;val c7=ml+512f
    val w1=78f;val w2=144f;val w3=98f;val w4=48f;val w5=48f;val w6=48f;val w7=cr-c7-8f
    fun dth(c:Canvas,y:Float):Float{val r=RectF(ml,y,cr,y+22f);c.drawRoundRect(r,8f,8f,hfp);c.drawRoundRect(r,8f,8f,bop);c.drawText("TICKET",c1,y+15f,hpp);c.drawText("ISSUE",c2,y+15f,hpp);c.drawText("CALLER",c3,y+15f,hpp);c.drawText("DEPT",c4,y+15f,hpp);c.drawText("PRIORITY",c5,y+15f,hpp);c.drawText("STATUS",c6,y+15f,hpp);c.drawText("AGE",c7,y+15f,hpp);return r.bottom+4f}
    var ps=npp();ps.y=dth(ps.c,ps.y)
    tickets.forEachIndexed{idx,rec->
        if(ps.y+34f>ph-42f){fpp(ps.p,ps.c,ps.n);ps=npp();ps.y=dth(ps.c,ps.y)}
        val rr=RectF(ml,ps.y,cr,ps.y+32f);ps.c.drawRoundRect(rr,6f,6f,if(idx%2==0)wrp else arp);ps.c.drawRoundRect(rr,6f,6f,bop)
        val my=ps.y+19f;val my2=ps.y+27f
        ps.c.drawText(ell(safe(rec.ticketCode),cp,w1),c1,my,cp)
        ps.c.drawText(ell(safe(rec.issue),cp,w2),c2,my,cp)
        ps.c.drawText(ell(safe(rec.callerName),mp,w3),c3,my,mp)
        ps.c.drawText(ell(safe(rec.department),mp,w4),c4,my,mp)
        val pdc=when(rec.priority?.lowercase(Locale.US)){"critical"->Color.rgb(183,28,28);"high"->Color.rgb(230,81,0);"medium"->Color.rgb(21,101,192);else->Color.rgb(27,94,32)}
        ps.c.drawCircle(c5+4f,ps.y+14f,3.5f,Paint(Paint.ANTI_ALIAS_FLAG).apply{color=pdc;style=Paint.Style.FILL})
        ps.c.drawText(ell(safe(rec.priority),cp,w5-14f),c5+14f,my,cp)
        val pr=RectF(c6,ps.y+8f,c6+50f,ps.y+22f);val sp=sbg(rec.status);val spt=stx(rec.status)
        ps.c.drawRoundRect(pr,5f,5f,sp);ps.c.drawText(ell(safe(rec.status),spt,40f),c6+5f,ps.y+18f,spt)
        ps.c.drawText("${rec.ticketAgeDays}d",c7,my,mp)
        ps.c.drawText(ell(safe(rec.issueType),mp,w7),c2,my2,mp)
        ps.y=rr.bottom+3f
    }
    fpp(ps.p,ps.c,ps.n)
    val fn="CallMonitoring_${tabTitle.replace(" ","_")}_${System.currentTimeMillis()}.pdf"
    savePdfAndOpen(context,doc,fn)
}
