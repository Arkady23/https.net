async function go(url2){
  var d=""; // пример без использования входных данных
  try{
    req=await fetch(url2,{
        method:'POST', headers:{'Content-Type':
        'application/x-www-form-urlencoded'},
        body: encodeURIComponent(d)
      })
    if(req) if(req.ok){
      go2();
    }else{
      alert("No se pudieron obtener los datos: "+req.statusText);
    }
  }catch(e){
    alert("No se pueden transferir datos: "+e.message);
  }
}
async function go2(){
  responseText = await req.text()
  document.getElementById("result_prg").innerHTML= responseText
}
