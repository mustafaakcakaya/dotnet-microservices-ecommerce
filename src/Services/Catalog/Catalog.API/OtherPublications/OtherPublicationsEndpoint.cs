using Carter;

namespace Catalog.API.OtherPublications;

public class OtherPublicationsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/other-publications", () =>
            Results.Content(Html, "text/html; charset=utf-8"));
    }

    // Note: Image assets are not part of this repo, so card "images" are represented with gradients.
    private const string Html = """
<!doctype html>
<html lang="tr">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>Diğer Yayınlar</title>
  <style>
    :root{
      --bg1:#f7d9d0;
      --bg2:#f7b99d;
      --card:#f6b28e;
      --ink:#2b2b2b;
      --muted: rgba(0,0,0,.55);
      --shadow: 0 18px 40px rgba(0,0,0,.12);
      --radius: 14px;
    }
    *{box-sizing:border-box}
    body{
      margin:0;
      font-family: ui-sans-serif, system-ui, -apple-system, Segoe UI, Roboto, Arial, "Apple Color Emoji","Segoe UI Emoji";
      color:var(--ink);
      background:
        radial-gradient(1200px 600px at 50% -120px, #ffffffaa 0%, transparent 60%),
        linear-gradient(135deg, var(--bg1), var(--bg2));
      min-height:100vh;
      display:flex;
      align-items:flex-start;
      justify-content:center;
    }

    .wrap{
      width:min(1100px, 100%);
      padding:40px 18px 60px;
      position:relative;
      overflow:hidden;
    }
    .leaves{
      position:absolute;
      left: -120px;
      right: -120px;
      bottom: -70px;
      height: 420px;
      background:
        radial-gradient(circle at 30% 70%, rgba(255,255,255,.35), transparent 55%),
        radial-gradient(circle at 60% 80%, rgba(255,255,255,.25), transparent 60%),
        linear-gradient(180deg, rgba(255,255,255,0) 0%, rgba(255,255,255,.35) 100%);
      pointer-events:none;
      filter: blur(.2px);
    }

    h1{
      text-align:center;
      margin: 0 0 22px;
      letter-spacing: .16em;
      font-size: 18px;
      font-weight: 800;
    }

    .cards{
      display:flex;
      gap: 26px;
      justify-content:center;
      align-items:stretch;
      flex-wrap:wrap;
    }

    .card{
      width: 320px;
      border-radius: var(--radius);
      box-shadow: var(--shadow);
      background: linear-gradient(180deg, #ffffff00 0%, #ffffff00 0%), var(--card);
      overflow:hidden;
      position:relative;
    }

    .img{
      height: 160px;
      background: linear-gradient(135deg, rgba(0,0,0,.08), rgba(0,0,0,.02)),
                  radial-gradient(circle at 20% 20%, rgba(255,255,255,.45), transparent 55%),
                  linear-gradient(120deg, #5ab1a6, #e3a38a);
      position:relative;
    }
    .img.one{ background:
      linear-gradient(135deg, rgba(0,0,0,.10), rgba(0,0,0,.02)),
      radial-gradient(circle at 25% 25%, rgba(255,255,255,.48), transparent 55%),
      linear-gradient(120deg, #4e9c86, #e7b39c);
    }
    .img.two{ background:
      linear-gradient(135deg, rgba(0,0,0,.10), rgba(0,0,0,.02)),
      radial-gradient(circle at 25% 25%, rgba(255,255,255,.48), transparent 55%),
      linear-gradient(120deg, #5aa1db, #e8b17e);
    }
    .img.three{ background:
      linear-gradient(135deg, rgba(0,0,0,.10), rgba(0,0,0,.02)),
      radial-gradient(circle at 25% 25%, rgba(255,255,255,.48), transparent 55%),
      linear-gradient(120deg, #c44a4a, #e6b28a);
    }

    .content{
      padding: 16px 18px 14px;
    }

    .date{
      font-size: 12px;
      color: var(--muted);
      margin-top: 6px;
    }
    .title{
      font-size: 18px;
      font-weight: 700;
      line-height: 1.25;
      margin: 0;
    }

    .footer{
      display:flex;
      justify-content:space-between;
      align-items:center;
      padding: 0 16px 14px;
      color: rgba(0,0,0,.55);
      font-size: 12px;
    }
    .actions{
      display:flex;
      gap: 10px;
      align-items:center;
    }
    .heart{
      width: 22px;
      height: 22px;
      display:inline-flex;
      align-items:center;
      justify-content:center;
      border-radius: 8px;
      background: rgba(255,255,255,.42);
      border: 1px solid rgba(0,0,0,.08);
      color: #b23b3b;
      font-weight: 800;
    }
    .share{
      color: rgba(0,0,0,.45);
      font-weight: 800;
    }

    /* make it closer to screenshot aspect */
    @media (max-width: 980px){
      .card{ width: min(360px, 100%); }
      .cards{ gap: 18px; }
    }
  </style>
</head>
<body>
  <div class="wrap">
    <div class="leaves"></div>
    <h1>Diger Yayinlar</h1>
    <div class="cards">
      <article class="card">
        <div class="img one"></div>
        <div class="content">
          <p class="title">Baglanma Kurami ile Guven ve Sekinet Iliskisi</p>
          <div class="date">- Aralik 13, 2025</div>
        </div>
        <div class="footer">
          <div class="actions">
            <span style="display:inline-block;width:10px;height:10px;border:2px solid rgba(0,0,0,.25);border-radius:2px"></span>
            <span>1</span>
          </div>
          <div class="actions">
            <span class="heart">H</span>
            <span class="share">SH</span>
          </div>
        </div>
      </article>

      <article class="card">
        <div class="img two"></div>
        <div class="content">
          <p class="title">Savaşların Sebebi Din mi?</p>
          <div class="date">- Temmuz 04, 2025</div>
        </div>
        <div class="footer">
          <div class="actions">
            <span style="display:inline-block;width:10px;height:10px;border:2px solid rgba(0,0,0,.25);border-radius:2px"></span>
            <span>0</span>
          </div>
          <div class="actions">
            <span class="heart">H</span>
            <span class="share">SH</span>
          </div>
        </div>
      </article>

      <article class="card">
        <div class="img three"></div>
        <div class="content">
          <p class="title">Vahsi Ideoloji: Fasizm</p>
          <div class="date">- Temmuz 28, 2024</div>
        </div>
        <div class="footer">
          <div class="actions">
            <span style="display:inline-block;width:10px;height:10px;border:2px solid rgba(0,0,0,.25);border-radius:2px"></span>
            <span>0</span>
          </div>
          <div class="actions">
            <span class="heart">H</span>
            <span class="share">SH</span>
          </div>
        </div>
      </article>
    </div>
  </div>
</body>
</html>
""";
}

