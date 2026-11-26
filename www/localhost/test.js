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
      mess("Не удалось получить данные: "+req.statusText);
    }
  }catch(e){
    mess("Невозможно передать данные: "+e.message);
  }
}
function mess(x){
  document.getElementById("result").innerHTML= x;
}
async function go2(){
  responseText = await req.text()
  mess(responseText)
}
